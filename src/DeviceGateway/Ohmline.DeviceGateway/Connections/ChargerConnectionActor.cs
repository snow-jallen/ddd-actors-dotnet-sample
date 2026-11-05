using System.Collections.Immutable;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Chargers;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.DeviceGateway.Diagnostics;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Messages;
using Ohmline.DeviceGateway.Ocpp.Translation;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts.Book;
using Malformed = Ohmline.DeviceGateway.Ocpp.Translation.Malformed;

namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// One charger's WebSocket, as an actor (section 16.7): frames in one at a time, translated by
/// the pure <see cref="OcppTranslator"/>, the commands forwarded to Charging's regions through
/// their proxies, and the charger answered once Charging has them. Charging's commands for the
/// charger arrive on its topic and go out as OCPP calls, each matched to its answer by message
/// id, or given up on after a timeout.
/// </summary>
/// <remarks>
/// The actor holds what the gateway knows about the charger (<see cref="ChargerContext"/>) and
/// nothing durable: when the socket closes, the actor stops, and a charger that reconnects gets
/// a new one. While a call's commands are on their way, other frames wait in the stash, so
/// Charging hears a charger's reports in the order the charger sent them.
/// </remarks>
public sealed class ChargerConnectionActor : TracedReceiveActor, IWithTimers, IWithStash
{
    private readonly IChargerLink _link;
    private readonly ChargerRegions _regions;
    private readonly GatewayOptions _options;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, PendingCall> _calls = [];
    private readonly Dictionary<string, string> _requests = [];
    private readonly Dictionary<string, ChargerCommandReply> _starts = [];
    private readonly Dictionary<int, long> _profiles = [];
    private readonly ILogger _log;
    private ChargerContext _context;

    public ChargerConnectionActor(
        ChargePointId chargePointId,
        IChargerLink link,
        ChargerRegions regions,
        GatewayOptions options,
        TimeProvider clock,
        ILogger<ChargerConnectionActor> log)
    {
        _link = link;
        _log = log;
        _regions = regions;
        _options = options;
        _clock = clock;
        _context = ChargerContext.For(chargePointId);
        Listening();
    }

    public ITimerScheduler Timers { get; set; } = null!;

    public IStash Stash { get; set; } = null!;

    /// <summary>
    /// The connection's actor; a test may leave the logger out, and its messages go nowhere.
    /// </summary>
    public static Props Props(
        ChargePointId chargePointId,
        IChargerLink link,
        ChargerRegions regions,
        GatewayOptions options,
        TimeProvider clock,
        ILogger<ChargerConnectionActor>? log = null)
    {
        var logger = log ?? NullLogger<ChargerConnectionActor>.Instance;
        return Akka.Actor.Props.Create(() => new ChargerConnectionActor(
            chargePointId, link, regions, options, clock, logger));
    }

    protected override void PreStart() => ExpectFrameWithinSilenceTimeout();

    /// <summary>Whatever was still waiting for the charger will not get an answer now.</summary>
    protected override void PostStop()
    {
        foreach (var pending in _calls.Values)
        {
            var gone = new ChargerCommandRejected(pending.ConnectorId, "The charger disconnected.");
            foreach (var asker in pending.ReplyTo)
            {
                asker.Tell(gone);
            }
        }

        _link.Close();
    }

    private void Listening()
    {
        Receive<FrameReceived>(Received);
        Receive<RemoteStart>(Start);
        Receive<RemoteStop>(StopRemotely);
        Receive<SetChargingProfile>(Profile);
        Receive<CallTimedOut>(timedOut => Answered(
            timedOut.MessageId,
            $"The charger did not answer within {_options.CallTimeout.TotalSeconds:0} s."));
        Receive<TermsKnown>(known => _context = _context with { Terms = known.Terms });
        Receive<TermsUnknown>(unknown =>
            _log.TermsUnknown(_context.ChargePointId, unknown.Cause));
        Receive<Silent>(_ => GiveUp());
        Receive<Disconnected>(_ => Context.Stop(Self));
    }

    /// <summary>A call's commands are on their way to Charging: everything else waits.</summary>
    private void Forwarding()
    {
        Receive<Forwarded>(forwarded =>
        {
            foreach (var rejected in forwarded.Rejected)
            {
                _log.ReportRefused(_context.ChargePointId, rejected.Message, rejected.Code);
            }

            Send(forwarded.Reply);
            BackToListening();
        });
        Receive<ForwardFailed>(failed =>
        {
            _log.ChargingDidNotAnswer(_context.ChargePointId, failed.Cause);
            _context = failed.Before; // Undone: the charger sends the call again.
            Send(new CallError(failed.MessageId, "InternalError", "Charging did not answer."));
            BackToListening();
        });
        Receive<Disconnected>(_ => Context.Stop(Self));
        ReceiveAny(_ => Stash.Stash());
    }

    private void BackToListening()
    {
        Become(Listening);
        Stash.UnstashAll();
    }

    private void Received(FrameReceived frame)
    {
        ExpectFrameWithinSilenceTimeout();
        switch (OcppFrameParser.Parse(frame.Text).Value)
        {
            case Ok<OcppFrame> { Value.Value: Call call }:
                Handle(call);
                break;
            case Ok<OcppFrame> { Value.Value: CallResult result }:
                Answered(
                    result.MessageId,
                    OutboundCalls.Accepted(result) ? null : "The charger refused.");
                break;
            case Ok<OcppFrame> { Value.Value: CallError error }:
                Answered(error.MessageId, $"{error.ErrorCode}: {error.Description}");
                break;
            case Err<FrameError> { Error: var error }:
                _log.BrokenFrame(_context.ChargePointId, error);
                if (error.Reply is { } reply)
                {
                    Send(reply);
                }

                break;
        }
    }

    private void Handle(Call call)
    {
        var commandId = CommandId.New(_clock);
        switch (OcppTranslator.Translate(call, _context, _clock.GetUtcNow(), commandId).Value)
        {
            case Translated translated:
                Translated(call, translated);
                break;
            case Ignored ignored:
                _log.CallIgnored(_context.ChargePointId, call.Action, ignored.Reason);
                Send(ignored.Reply);
                break;
            case Malformed malformed:
                _log.CallMalformed(_context.ChargePointId, call.Action, malformed.Reason);
                Send(malformed.Reply);
                break;
            case null:
                throw new InvalidOperationException("The translation is default: no case.");
        }
    }

    private void Translated(Call call, Translated translated)
    {
        var before = _context;
        _context = translated.Context;
        switch (call.Action)
        {
            case OcppActions.BootNotification:
                Booted();
                break;
            case OcppActions.Heartbeat:
                AskForTerms(); // A tariff published since the last heartbeat applies from now.
                break;
        }

        if (translated.Commands.IsEmpty)
        {
            Send(translated.Reply);
            return;
        }

        SendToChargingAsync(_regions, translated.Commands, _options.AskTimeout).PipeTo(
            Self,
            success: rejected => new Forwarded(translated.Reply, rejected),
            failure: cause => new ForwardFailed(call.MessageId, before, cause));
        Become(Forwarding);
    }

    /// <summary>
    /// The commands one at a time, in order, each to its region, each waiting for its answer:
    /// the charge point occupied before its session starts.
    /// </summary>
    private static async Task<ImmutableArray<CommandRejected>> SendToChargingAsync(
        ChargerRegions regions, ImmutableArray<ChargingCommand> commands, TimeSpan timeout)
    {
        var rejected = ImmutableArray.CreateBuilder<CommandRejected>();
        foreach (var command in commands)
        {
            var reply = command.Value switch
            {
                ChargePointCommand forChargePoint =>
                    await regions.ChargePoints.AskTraced<CommandReply>(forChargePoint, timeout),
                SessionCommand forSession =>
                    await regions.Sessions.AskTraced<CommandReply>(forSession, timeout),
                _ => throw new InvalidOperationException("The command is default: no case."),
            };
            if (reply is CommandRejected refusal)
            {
                rejected.Add(refusal);
            }
        }

        return rejected.ToImmutable();
    }

    /// <summary>
    /// The charger said who it is: from now on Charging's commands for it reach this actor, on
    /// the charge point's topic, wherever in the cluster they are sent from.
    /// </summary>
    private void Booted()
    {
        _regions.Mediator.Tell(new Subscribe(ChargerTopics.Of(_context.ChargePointId), Self));
        _log.Booted(_context.ChargePointId);
        AskForTerms();
    }

    /// <summary>The charge point's site and the tariff now, for sessions started here.</summary>
    private void AskForTerms()
    {
        TermsAsync(_regions, _context.ChargePointId, _options).PipeTo(
            Self,
            success: terms => new TermsKnown(terms),
            failure: cause => new TermsUnknown(cause));

        static async Task<StartingTerms?> TermsAsync(
            ChargerRegions regions, ChargePointId chargePointId, GatewayOptions options)
        {
            var status = await regions.ChargePoints.AskTraced<ChargePointStatus>(
                new GetChargePointStatus(chargePointId), options.AskTimeout);
            var current = await regions.TariffBook.AskTraced<CurrentTariff>(
                new GetTariffSnapshot(new(options.TariffId)), options.AskTimeout);
            return (status.SiteId, current.Snapshot) is ({ } site, { } tariff)
                ? new StartingTerms(site, tariff, new Money(options.SpendCap, tariff.Currency))
                : null;
        }
    }

    /// <summary>
    /// The journey's start, as a RemoteStartTransaction. The same start asked again under the
    /// same key is answered as it was the first time, and the charger is not asked twice.
    /// </summary>
    private void Start(RemoteStart start)
    {
        if (_starts.TryGetValue(start.IdempotencyKey, out var answered))
        {
            Sender.Tell(answered);
            return;
        }

        var connector = start.ConnectorId.Number;
        Ask(
            start.IdempotencyKey,
            start.ConnectorId,
            id =>
            {
                _context = _context.Expecting(connector, start.SessionId, start.TokenUid);
                return OutboundCalls.RemoteStart(
                    id, connector, start.TokenUid, start.Limit, profileId: 1);
            },
            pending => pending with { StartKey = start.IdempotencyKey });
    }

    private void StopRemotely(RemoteStop stop)
    {
        var transaction = _context.Transactions.Values
            .FirstOrDefault(transaction => transaction.SessionId == stop.SessionId);
        if (transaction is null)
        {
            Sender.Tell(new ChargerCommandRejected(
                stop.ConnectorId, $"No OCPP transaction is session {stop.SessionId}."));
            return;
        }

        Ask(
            $"stop-{stop.SessionId}",
            stop.ConnectorId,
            id => OutboundCalls.RemoteStop(id, transaction.TransactionId),
            pending => pending);
    }

    /// <summary>
    /// A new allocation for the connector. One the charger already has, or an older one, is
    /// answered at once: at least once from the dispatcher, applied once here (section 16.8).
    /// </summary>
    private void Profile(SetChargingProfile profile)
    {
        var connector = profile.ConnectorId.Number;
        if (_profiles.TryGetValue(connector, out var applied) && applied >= profile.Version)
        {
            Sender.Tell(new ChargerCommandAccepted(profile.ConnectorId));
            return;
        }

        Ask(
            $"profile-{connector}-{profile.Version}",
            profile.ConnectorId,
            id => OutboundCalls.SetChargingProfile(id, connector, profile.Limit, profile.Version),
            pending => pending with { ProfileVersion = profile.Version });
    }

    /// <summary>
    /// The call goes out, and its answer or timeout comes back by its message id. The same
    /// request asked again while the charger has not answered is not sent again: the asker
    /// joins the call, and hears the one answer with everyone else.
    /// </summary>
    private void Ask(
        string request,
        ConnectorId connectorId,
        Func<string, Call> call,
        Func<PendingCall, PendingCall> details)
    {
        if (_requests.TryGetValue(request, out var inFlight))
        {
            _calls[inFlight] = _calls[inFlight].AlsoFor(Sender);
            return;
        }

        var outgoing = call(NewMessageId());
        _requests[request] = outgoing.MessageId;
        _calls[outgoing.MessageId] = details(new PendingCall(request, [Sender], connectorId));
        Timers.StartSingleTimer(
            outgoing.MessageId, new CallTimedOut(outgoing.MessageId), _options.CallTimeout);
        Send(outgoing);
    }

    /// <summary>The charger answered a call, or never will: null if it accepted.</summary>
    private void Answered(string messageId, string? refusal)
    {
        if (!_calls.Remove(messageId, out var pending))
        {
            _log.UnexpectedAnswer(_context.ChargePointId, messageId);
            return;
        }

        Timers.Cancel(messageId);
        _requests.Remove(pending.Request);
        ChargerCommandReply reply = refusal is null
            ? new ChargerCommandAccepted(pending.ConnectorId)
            : new ChargerCommandRejected(pending.ConnectorId, refusal);
        foreach (var asker in pending.ReplyTo)
        {
            asker.Tell(reply);
        }

        if (pending.StartKey is { } key)
        {
            _starts[key] = reply;
            if (refusal is not null)
            {
                _context = _context with
                {
                    PendingStarts = _context.PendingStarts.Remove(pending.ConnectorId.Number),
                };
            }
        }

        if (pending.ProfileVersion is { } version && refusal is null)
        {
            _profiles[pending.ConnectorId.Number] = version;
        }
    }

    private void ExpectFrameWithinSilenceTimeout() =>
        Timers.StartSingleTimer(nameof(Silent), Silent.Instance, _options.SilenceTimeout);

    private void GiveUp()
    {
        _log.Silent(_context.ChargePointId, _options.SilenceTimeout);
        Context.Stop(Self);
    }

    private void Send(OcppFrame frame) => _link.Send(OcppFrameParser.Write(frame));

    private static string NewMessageId() => Guid.NewGuid().ToString("N");
}
