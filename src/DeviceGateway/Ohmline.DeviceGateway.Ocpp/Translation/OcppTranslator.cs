using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Messages;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DeviceGateway.Ocpp.Translation;

/// <summary>
/// The Device Gateway's anticorruption layer (section 16.6): a charger's call in, Charging's
/// commands and the charger's answer out. A pure function of the call, what the gateway
/// remembers about the charger, the time and a command id: no actor, no socket, no clock.
/// </summary>
/// <remarks>
/// OCPP's words stop here. An OCPP "transaction" becomes an Ohmline session; registers become
/// Wh whatever unit the vendor sent; a token is upper-cased; a time without an offset is UTC.
/// </remarks>
public static class OcppTranslator
{
    /// <summary>How often a charger is asked to send a heartbeat.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);

    private const string EnergyRegister = "Energy.Active.Import.Register";

    /// <summary>An answer with nothing in it: <c>{}</c>.</summary>
    private static readonly object Empty = new();

    public static Translation Translate(
        Call call, ChargerContext context, DateTimeOffset now, CommandId commandId)
    {
        try
        {
            return call.Action switch
            {
                OcppActions.BootNotification => Boot(call, context, now),
                OcppActions.Heartbeat => Answer(
                    call, context, new HeartbeatResponse(now)),
                OcppActions.StatusNotification => Status(
                    call, Read<StatusNotificationRequest>(call), context, commandId),
                OcppActions.Authorize => Authorize(call, Read<AuthorizeRequest>(call), context),
                OcppActions.StartTransaction => Start(
                    call, Read<StartTransactionRequest>(call), context, commandId),
                OcppActions.MeterValues => Meter(
                    call, Read<MeterValuesRequest>(call), context, commandId),
                OcppActions.StopTransaction => Stop(
                    call, Read<StopTransactionRequest>(call), context, commandId),
                var action => new Ignored(
                    $"{action} is not in the subset the gateway speaks.",
                    new CallError(call.MessageId, "NotImplemented", $"{action} is not supported.")),
            };
        }
        catch (Exception unreadable) when (unreadable is JsonException or FormatException)
        {
            return new Malformed(
                unreadable.Message,
                new CallError(call.MessageId, "FormationViolation", unreadable.Message));
        }
    }

    private static Translated Boot(Call call, ChargerContext context, DateTimeOffset now)
    {
        _ = Read<BootNotificationRequest>(call); // Read only to reject a malformed boot.
        return Answer(
            call,
            context,
            new BootNotificationResponse("Accepted", now, (int)HeartbeatInterval.TotalSeconds));
    }

    /// <summary>
    /// A status is a command only as a change: a fault, a repair, a car gone, a pause or a
    /// resume of the session on the connector. The rest is remembered and answered. The charger
    /// says <c>Faulted</c>, in OCPP's words; Charging hears that the connector is out of order,
    /// in its own (section 20.7).
    /// </summary>
    private static Translation Status(
        Call call, StatusNotificationRequest status, ChargerContext context, CommandId commandId)
    {
        if (status.ConnectorId == 0)
        {
            return new Ignored(
                "Connector 0 is the charge point as a whole.", Reply(call, Empty));
        }

        var connector = status.ConnectorId;
        var before = context.Statuses.GetValueOrDefault(connector);
        var session = context.OnConnector(connector)?.SessionId;
        var chargePoint = context.ChargePointId;
        ChargingCommand? change = (before, status.Status, session) switch
        {
            (_, "Faulted", _) =>
                new ReportConnectorOutOfOrder(commandId, chargePoint, connector, status.ErrorCode),
            ("Faulted", "Available", _) =>
                new ReportConnectorRepaired(commandId, chargePoint, connector),
            ("Finishing", "Available", _) =>
                new ReleaseConnector(commandId, chargePoint, connector),
            (_, "SuspendedEV", { } suspended) =>
                new SuspendCharging(commandId, suspended, new ByEv()),
            (_, "SuspendedEVSE", { } suspended) =>
                new SuspendCharging(commandId, suspended, new ByCharger()),
            ("SuspendedEV" or "SuspendedEVSE", "Charging", { } resumed) =>
                new ResumeCharging(commandId, resumed),
            _ => null,
        };

        return new Translated(
            change is { } changed ? [changed] : [],
            Reply(call, Empty),
            context with { Statuses = context.Statuses.SetItem(connector, status.Status) });
    }

    /// <summary>
    /// The gateway checks only that the token is one: whether it may charge is Driver Accounts'
    /// decision, when the journey locks it.
    /// </summary>
    private static Translated Authorize(
        Call call, AuthorizeRequest authorize, ChargerContext context) =>
        Answer(call, context, new AuthorizeResponse(
            TokenUid.TryCreate(authorize.IdTag).IsOk ? IdTagInfo.Accepted : IdTagInfo.Invalid));

    /// <summary>
    /// The car draws power. If Ohmline asked for this start, the OCPP transaction is the session
    /// the journey named, and the connector is occupied by it; a start Ohmline did not ask for
    /// is refused, because only a journey holds the money a session needs.
    /// </summary>
    private static Translation Start(
        Call call, StartTransactionRequest start, ChargerContext context, CommandId command)
    {
        var connector = start.ConnectorId;
        if (TokenUid.TryCreate(start.IdTag) is not Ok<TokenUid> { Value: var token })
        {
            return Refuse(call, $"'{start.IdTag}' is not a token.");
        }

        if (context.OnConnector(connector) is { } running && running.TokenUid == token)
        {
            return Answer(  // The same start sent again: the same transaction, nothing new.
                call,
                context,
                new StartTransactionResponse(running.TransactionId, IdTagInfo.Accepted));
        }

        if (context.PendingStarts.GetValueOrDefault(connector) is not { } pending
            || pending.TokenUid != token)
        {
            return Refuse(call, $"No journey asked for {token} on connector {connector}.");
        }

        if (context.Terms is not { } terms)
        {
            return Refuse(call, "The gateway does not know this charger's site or tariff yet.");
        }

        var transaction = new OpenTransaction(
            TransactionIds.Of(pending.SessionId), connector, pending.SessionId, token);
        var connectorId = new ConnectorId(context.ChargePointId, connector);
        return new Translated(
            [
                new OccupyConnector(
                    CommandId.Derive(command, "occupy"),
                    context.ChargePointId,
                    connector,
                    pending.SessionId,
                    token),
                new StartSession(
                    CommandId.Derive(command, "start"),
                    pending.SessionId,
                    terms.SiteId,
                    connectorId,
                    token,
                    terms.Tariff,
                    terms.SpendCap,
                    start.MeterStart),
            ],
            Reply(call, new StartTransactionResponse(
                transaction.TransactionId, IdTagInfo.Accepted)),
            context with
            {
                PendingStarts = context.PendingStarts.Remove(connector),
                Transactions = context.Transactions.SetItem(transaction.TransactionId, transaction),
            });
    }

    /// <summary>Each energy register read, in Wh, for the session on the connector.</summary>
    private static Translation Meter(
        Call call, MeterValuesRequest meter, ChargerContext context, CommandId command)
    {
        var transaction = meter.TransactionId is { } id
            ? context.Transaction(id)
            : context.OnConnector(meter.ConnectorId);
        if (transaction is null)
        {
            return new Ignored(
                $"No OCPP transaction the gateway knows on connector {meter.ConnectorId}.",
                Reply(call, new MeterValuesResponse()));
        }

        var readings = meter.MeterValue
            .SelectMany(value => value.SampledValue
                .Where(sample => (sample.Measurand ?? EnergyRegister) == EnergyRegister)
                .Select(sample => (value.Timestamp, Wh: WattHours(sample))))
            .Select((reading, index) => (ChargingCommand)new RecordMeterValue(
                CommandId.Derive(command, $"meter-{index}"),
                transaction.SessionId,
                reading.Wh,
                reading.Timestamp));
        return new Translated([.. readings], Reply(call, new MeterValuesResponse()), context);
    }

    /// <summary>The last register reading, then the end of the session.</summary>
    private static Translation Stop(
        Call call, StopTransactionRequest stop, ChargerContext context, CommandId command)
    {
        if (context.Transaction(stop.TransactionId) is not { } transaction)
        {
            return new Ignored(
                $"No OCPP transaction {stop.TransactionId} the gateway knows.",
                Reply(call, new StopTransactionResponse(IdTagInfo.Accepted)));
        }

        return new Translated(
            [
                new RecordMeterValue(
                    CommandId.Derive(command, "meter-stop"),
                    transaction.SessionId,
                    stop.MeterStop,
                    stop.Timestamp),
                new StopSession(CommandId.Derive(command, "stop"), transaction.SessionId),
            ],
            Reply(call, new StopTransactionResponse(IdTagInfo.Accepted)),
            context with { Transactions = context.Transactions.Remove(stop.TransactionId) });
    }

    /// <summary>
    /// A vendor quirk: most chargers report Wh, some kWh with decimals. Both become whole Wh.
    /// </summary>
    private static long WattHours(SampledValue sample)
    {
        var value = decimal.Parse(sample.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return sample.Unit switch
        {
            null or "Wh" => (long)decimal.Round(value, MidpointRounding.AwayFromZero),
            "kWh" => (long)decimal.Round(value * 1000m, MidpointRounding.AwayFromZero),
            var unit => throw new FormatException($"A register in {unit} is not energy."),
        };
    }

    private static Ignored Refuse(Call call, string reason) =>
        new(reason, Reply(call, new StartTransactionResponse(0, IdTagInfo.Invalid)));

    private static Translated Answer<T>(Call call, ChargerContext context, T payload) =>
        new([], Reply(call, payload), context);

    private static CallResult Reply<T>(Call call, T payload) =>
        new(call.MessageId, OcppJson.Element(payload));

    private static T Read<T>(Call call) =>
        call.Payload.Deserialize<T>(OcppJson.Options)
            ?? throw new JsonException($"{call.Action} has no payload.");

}
