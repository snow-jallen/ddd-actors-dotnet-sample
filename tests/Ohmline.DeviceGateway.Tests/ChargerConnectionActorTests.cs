using System.Text.Json;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Serialization;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Chargers;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.DeviceGateway.Connections;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Actors.Serialization;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Testing;

namespace Ohmline.DeviceGateway.Tests;

/// <summary>
/// One charger's connection actor between a recorded charger and fake Charging: probes as the
/// charge point and session regions, the tariff book and the pub-sub mediator.
/// </summary>
public sealed class ChargerConnectionActorTests(ITestOutputHelper output)
    : TestKit(
        ActorTestConfig.With(ChargingSerializers, TariffsSerializers),
        nameof(ChargerConnectionActorTests),
        output)
{
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");
    private static readonly ConnectorId Connector = new(Charger, 1);
    private static readonly TokenUid Card = new("04A2B3C4D5E6F7");
    private static readonly SessionId Session = new(Guid.CreateVersion7());
    private static readonly SiteId Depot = new("slc-depot");

    private static readonly TariffSnapshot Tariff = new(
        new TariffId("slc-standard"),
        1,
        Currency.Usd,
        [new EnergyRate(new Money(0.42m, Currency.Usd))],
        PriceCap: null,
        RoundingPolicy.Cents,
        "America/Denver");

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private TestProbe _chargerSide = null!;
    private TestProbe _chargePoints = null!;
    private TestProbe _sessions = null!;
    private TestProbe _mediator = null!;
    private RecordingLink _link = null!;

    [Fact]
    public async Task Given_a_boot_then_the_charger_is_accepted_and_its_topic_is_subscribed()
    {
        var connection = Connect();

        connection.Tell(Boot("b-1"));

        var answer = await AnswerAsync("b-1");
        answer.GetProperty("status").GetString().ShouldBe("Accepted");
        (await _mediator.ExpectMsgAsync<Subscribe>(cancellationToken: Ct)).Topic
            .ShouldBe("charger-commands-OHM-SLC-0042");
    }

    [Fact]
    public async Task Given_a_remote_start_then_the_charger_is_asked_and_its_answer_comes_back()
    {
        var connection = await BootedAsync();
        var journey = CreateTestProbe();

        connection.Tell(Start("key-1"), journey.Ref);
        var call = await CallAsync("RemoteStartTransaction");
        connection.Tell(Frame($$"""[3,"{{call.MessageId}}",{"status":"Accepted"}]"""));

        await journey.ExpectMsgAsync(new ChargerCommandAccepted(Connector), cancellationToken: Ct);
        call.Payload.GetProperty("idTag").GetString().ShouldBe(Card.Value);
    }

    [Fact]
    public async Task Given_the_same_start_asked_again_then_the_charger_is_asked_once()
    {
        var connection = await BootedAsync();
        var (first, again, later) = (CreateTestProbe(), CreateTestProbe(), CreateTestProbe());

        connection.Tell(Start("key-2"), first.Ref);
        var call = await CallAsync("RemoteStartTransaction");
        connection.Tell(Start("key-2"), again.Ref);
        await _chargerSide.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), Ct);
        connection.Tell(Frame($$"""[3,"{{call.MessageId}}",{"status":"Accepted"}]"""));
        connection.Tell(Start("key-2"), later.Ref);

        foreach (var asker in new[] { first, again, later })
        {
            await asker.ExpectMsgAsync(
                new ChargerCommandAccepted(Connector), cancellationToken: Ct);
        }

        await _chargerSide.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), Ct);
    }

    [Fact]
    public async Task Given_a_charger_that_never_answers_then_the_start_is_refused_after_a_while()
    {
        var connection = await BootedAsync();
        var journey = CreateTestProbe();

        connection.Tell(Start("key-3"), journey.Ref);
        await CallAsync("RemoteStartTransaction");

        var refused = await journey.ExpectMsgAsync<ChargerCommandRejected>(cancellationToken: Ct);
        refused.Reason.ShouldStartWith("The charger did not answer within");
    }

    [Fact]
    public async Task Given_a_started_session_then_Charging_hears_it_in_the_chargers_order()
    {
        var connection = await BootedAsync();
        var journey = CreateTestProbe();
        connection.Tell(Start("key-4"), journey.Ref);
        var call = await CallAsync("RemoteStartTransaction");
        connection.Tell(Frame($$"""[3,"{{call.MessageId}}",{"status":"Accepted"}]"""));
        await journey.ExpectMsgAsync<ChargerCommandAccepted>(cancellationToken: Ct);

        connection.Tell(Called("s-1", "StartTransaction", new
        {
            connectorId = 1,
            idTag = "04a2b3c4d5e6f7",
            meterStart = 1_200_000,
            timestamp = "2026-11-04T14:40:00Z",
        }));
        connection.Tell(Called("s-2", "MeterValues", new
        {
            connectorId = 1,
            meterValue = new[]
            {
                new
                {
                    timestamp = "2026-11-04T15:00:00Z",
                    sampledValue = new[] { new { value = "1210500" } },
                },
            },
        }));

        var occupy = await _chargePoints.ExpectMsgAsync<OccupyConnector>(cancellationToken: Ct);
        occupy.SessionId.ShouldBe(Session);
        await _sessions.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), Ct);
        _chargePoints.Reply(new CommandAccepted(Version: 2));
        var start = await _sessions.ExpectMsgAsync<StartSession>(cancellationToken: Ct);
        (start.SiteId, start.Tariff, start.SpendCap).ShouldBe(
            (Depot, Tariff, new Money(50m, Currency.Usd)));
        await _chargerSide.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), Ct);
        _sessions.Reply(new CommandAccepted(Version: 1));

        (await AnswerAsync("s-1")).GetProperty("transactionId").GetInt32().ShouldBePositive();
        (await _sessions.ExpectMsgAsync<RecordMeterValue>(cancellationToken: Ct)).RegisterWh
            .ShouldBe(1_210_500);
    }

    [Fact]
    public async Task Given_an_allocation_the_charger_already_has_then_it_is_not_told_again()
    {
        var connection = await BootedAsync();
        var dispatcher = CreateTestProbe();
        connection.Tell(new SetChargingProfile(Connector, new Power(11m), 3), dispatcher.Ref);
        var call = await CallAsync("SetChargingProfile");
        connection.Tell(Frame($$"""[3,"{{call.MessageId}}",{"status":"Accepted"}]"""));
        await dispatcher.ExpectMsgAsync<ChargerCommandAccepted>(cancellationToken: Ct);

        connection.Tell(new SetChargingProfile(Connector, new Power(11m), 3), dispatcher.Ref);
        connection.Tell(new SetChargingProfile(Connector, new Power(22m), 2), dispatcher.Ref);

        await dispatcher.ExpectMsgAsync<ChargerCommandAccepted>(cancellationToken: Ct);
        await dispatcher.ExpectMsgAsync<ChargerCommandAccepted>(cancellationToken: Ct);
        await _chargerSide.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), Ct);
    }

    [Fact]
    public async Task Given_a_charger_that_falls_silent_then_its_connection_is_closed()
    {
        var connection = Connect(silence: TimeSpan.FromMilliseconds(300));
        Watch(connection);

        await ExpectTerminatedAsync(connection, cancellationToken: Ct);

        _link.Closed.ShouldBeTrue();
    }

    [Fact]
    public async Task Given_a_broken_frame_with_an_id_then_the_charger_gets_an_error()
    {
        var connection = Connect();

        connection.Tell(Frame("""[2,"x-1","Heartbeat"]"""));

        var error = (await _chargerSide.ExpectMsgAsync<SentFrame>(cancellationToken: Ct)).Text;
        error.ShouldStartWith("""[4,"x-1","FormationViolation",""");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IActorRef Connect(TimeSpan? silence = null)
    {
        _chargerSide = CreateTestProbe();
        _chargePoints = CreateTestProbe();
        _sessions = CreateTestProbe();
        _mediator = CreateTestProbe();
        var tariffBook = CreateTestProbe();
        tariffBook.SetAutoPilot(new DelegateAutoPilot((sender, _) =>
        {
            sender.Tell(new CurrentTariff(Tariff.TariffId, Tariff));
            return AutoPilot.KeepRunning;
        }));
        _link = new RecordingLink(_chargerSide.Ref);
        var options = new GatewayOptions
        {
            CallTimeout = TimeSpan.FromMilliseconds(500),
            SilenceTimeout = silence ?? TimeSpan.FromMinutes(1),
        };
        var regions = new ChargerRegions(
            _chargePoints.Ref, _sessions.Ref, tariffBook.Ref, _mediator.Ref);
        return Sys.ActorOf(ChargerConnectionActor.Props(Charger, _link, regions, options, _clock));
    }

    /// <summary>A connected charger that has booted, and whose site and tariff are known.</summary>
    private async Task<IActorRef> BootedAsync()
    {
        var connection = Connect();
        connection.Tell(Boot("b-0"));
        await AnswerAsync("b-0");
        await _chargePoints.ExpectMsgAsync<GetChargePointStatus>(cancellationToken: Ct);
        _chargePoints.Reply(new ChargePointStatus(Charger, 1, Depot, []));
        await _mediator.ExpectMsgAsync<Subscribe>(cancellationToken: Ct);
        return connection;
    }

    private static RemoteStart Start(string key) =>
        new(Connector, Card, Session, new Power(22m), key);

    private static FrameReceived Frame(string text) => new(text);

    private static FrameReceived Boot(string messageId) => Called(
        messageId,
        "BootNotification",
        new { chargePointVendor = "Voltaris", chargePointModel = "VX-22" });

    /// <summary>A call from the charger, as it arrives on the socket.</summary>
    private static FrameReceived Called(string messageId, string action, object payload) =>
        Frame(OcppFrameParser.Write(
            new Call(messageId, action, JsonSerializer.SerializeToElement(payload))));

    private async Task<JsonElement> AnswerAsync(string messageId)
    {
        var sent = await _chargerSide.ExpectMsgAsync<SentFrame>(cancellationToken: Ct);
        var result = OcppFrameParser.Parse(sent.Text).Value.ShouldBeOfType<Ok<OcppFrame>>()
            .Value.Value.ShouldBeOfType<CallResult>();
        result.MessageId.ShouldBe(messageId);
        return result.Payload;
    }

    private async Task<Call> CallAsync(string action)
    {
        var sent = await _chargerSide.ExpectMsgAsync<SentFrame>(cancellationToken: Ct);
        var call = OcppFrameParser.Parse(sent.Text).Value.ShouldBeOfType<Ok<OcppFrame>>()
            .Value.Value.ShouldBeOfType<Call>();
        call.Action.ShouldBe(action);
        return call;
    }

    private static SerializerDetails ChargingSerializers(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            ChargingSerializer.Name,
            new ChargingSerializer(system),
            [.. ChargingSerializer.BoundTypes]);

    private static SerializerDetails TariffsSerializers(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            TariffsSerializer.Name,
            new TariffsSerializer(system),
            [.. TariffsSerializer.BoundTypes]);
}
