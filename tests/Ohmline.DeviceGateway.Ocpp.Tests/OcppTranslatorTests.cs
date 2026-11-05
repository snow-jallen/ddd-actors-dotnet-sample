using System.Text.Json;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Translation;
using Ohmline.SharedKernel.Identity;
using static Ohmline.DeviceGateway.Ocpp.Tests.Recorded;

namespace Ohmline.DeviceGateway.Ocpp.Tests;

/// <summary>
/// Recorded frames in, Charging's commands and the charger's answers out. Each vendor's quirks
/// come out the same as the protocol written to the letter.
/// </summary>
public sealed class OcppTranslatorTests
{
    private static readonly DateTimeOffset ThreePm = new(2026, 11, 4, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("voltaris")]
    [InlineData("chargewell")]
    public void Given_a_boot_then_it_is_accepted_with_a_heartbeat_every_minute(string vendor)
    {
        var translated = Translate(vendor, "boot-notification", Expecting)
            .Value.ShouldBeOfType<Translated>();

        translated.Commands.ShouldBeEmpty();
        Payload(translated.Reply).GetProperty("status").GetString().ShouldBe("Accepted");
        Payload(translated.Reply).GetProperty("interval").GetInt32().ShouldBe(60);
    }

    [Theory]
    [InlineData("voltaris")]
    [InlineData("chargewell")]
    public void Given_the_start_a_journey_asked_for_then_its_session_starts_on_the_connector(
        string vendor)
    {
        var translated = Translate(vendor, "start-transaction", Expecting)
            .Value.ShouldBeOfType<Translated>();

        Sent(translated).ShouldBe(
        [
            new OccupyConnector(Derived("occupy"), Charger, 1, Session, DriversCard),
            new StartSession(
                Derived("start"),
                Session,
                Terms.SiteId,
                new ConnectorId(Charger, 1),
                DriversCard,
                Terms.Tariff,
                Terms.SpendCap,
                MeterStartWh: 1_200_000),
        ]);
        Payload(translated.Reply).GetProperty("transactionId").GetInt32().ShouldBe(220089095);
        translated.Context.PendingStarts.ShouldBeEmpty();
        translated.Context.OnConnector(1).ShouldBe(
            new OpenTransaction(220089095, 1, Session, DriversCard));
    }

    [Fact]
    public void Given_a_start_no_journey_asked_for_then_the_token_is_refused()
    {
        var context = ChargerContext.For(Charger) with { Terms = Terms };

        var ignored = Translate("voltaris", "start-transaction", context)
            .Value.ShouldBeOfType<Ignored>();

        IdTagStatus(ignored.Reply).ShouldBe("Invalid");
    }

    [Fact]
    public void Given_a_charger_whose_site_and_tariff_are_not_known_yet_then_a_start_is_refused()
    {
        var context = Expecting with { Terms = null };

        var ignored = Translate("voltaris", "start-transaction", context)
            .Value.ShouldBeOfType<Ignored>();

        IdTagStatus(ignored.Reply).ShouldBe("Invalid");
    }

    [Fact]
    public void Given_a_start_sent_again_then_it_gets_the_same_transaction_and_nothing_new()
    {
        var again = Translate("chargewell", "start-transaction", MidSession)
            .Value.ShouldBeOfType<Translated>();

        again.Commands.ShouldBeEmpty();
        Payload(again.Reply).GetProperty("transactionId").GetInt32().ShouldBe(220089095);
    }

    [Theory]
    [InlineData("voltaris")] // 1210500 Wh, by transaction id, with a power reading beside it.
    [InlineData("chargewell")] // 1210.5 kWh, by connector, at a time without an offset.
    public void Given_a_meter_reading_then_the_session_records_its_register_in_Wh(string vendor)
    {
        var translated = Translate(vendor, "meter-values", MidSession)
            .Value.ShouldBeOfType<Translated>();

        Sent(translated).ShouldBe(
            [new RecordMeterValue(Derived("meter-0"), Session, 1_210_500, ThreePm)]);
    }

    [Fact]
    public void Given_a_reading_for_a_transaction_the_gateway_does_not_know_then_it_is_ignored()
    {
        Translate("voltaris", "meter-values", Expecting).Value.ShouldBeOfType<Ignored>();
    }

    [Fact]
    public void Given_a_stop_then_the_last_reading_is_recorded_and_the_session_stopped()
    {
        var translated = Translate("voltaris", "stop-transaction", MidSession)
            .Value.ShouldBeOfType<Translated>();

        Sent(translated).ShouldBe(
        [
            new RecordMeterValue(
                Derived("meter-stop"), Session, 1_230_000, ThreePm.AddMinutes(25)),
            new StopSession(Derived("stop"), Session),
        ]);
        translated.Context.Transactions.ShouldBeEmpty();
    }

    public static TheoryData<string?, string, string, string?> Statuses => new()
    {
        // status before, status now, error code, then
        { null, "Faulted", "GroundFailure", "ReportConnectorOutOfOrder GroundFailure" },
        { "Faulted", "Available", "NoError", "ReportConnectorRepaired" },
        { "Finishing", "Available", "NoError", "ReleaseConnector" },
        { "Charging", "SuspendedEV", "NoError", "SuspendCharging ByEv" },
        { "Charging", "SuspendedEVSE", "NoError", "SuspendCharging ByCharger" },
        { "SuspendedEV", "Charging", "NoError", "ResumeCharging" },
        { "Available", "Preparing", "NoError", null },
        { "Charging", "Finishing", "NoError", null },
        { "Available", "Available", "NoError", null },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public void A_status_is_a_command_only_when_it_changes_something_Charging_knows(
        string? before, string now, string errorCode, string? then)
    {
        var context = before is null
            ? MidSession
            : MidSession with { Statuses = MidSession.Statuses.SetItem(1, before) };
        var payload = $$"""{"connectorId":1,"errorCode":"{{errorCode}}","status":"{{now}}"}""";
        var call = new Call("s-1", "StatusNotification", JsonDocument.Parse(payload).RootElement);

        var translated = OcppTranslator.Translate(call, context, Morning, Command)
            .Value.ShouldBeOfType<Translated>();

        Sent(translated).Select(Show).SingleOrDefault().ShouldBe(then);
        translated.Context.Statuses[1].ShouldBe(now);
    }

    [Fact]
    public void Given_a_recorded_fault_then_the_connector_reports_the_chargers_code()
    {
        var translated = Translate("voltaris", "status-faulted", MidSession)
            .Value.ShouldBeOfType<Translated>();

        Sent(translated).ShouldBe(
            [new ReportConnectorOutOfOrder(Command, Charger, 2, "GroundFailure")]);
    }

    [Fact]
    public void Given_an_action_outside_the_subset_then_it_is_answered_not_implemented()
    {
        var ignored = Translate("chargewell", "data-transfer", MidSession)
            .Value.ShouldBeOfType<Ignored>();

        ignored.Reply.Value.ShouldBeOfType<CallError>().ErrorCode.ShouldBe("NotImplemented");
    }

    [Fact]
    public void Given_a_payload_that_does_not_fit_its_action_then_it_is_malformed()
    {
        var malformed = Translate("chargewell", "start-transaction-malformed", Expecting)
            .Value.ShouldBeOfType<Malformed>();

        malformed.Reply.ErrorCode.ShouldBe("FormationViolation");
        malformed.Reply.MessageId.ShouldBe("cw-0005");
    }

    private static CommandId Derived(string purpose) => CommandId.Derive(Command, purpose);

    private static object[] Sent(Translated translated) =>
        [.. translated.Commands.Select(command => command.Value!)];

    private static string Show(object command) => command switch
    {
        ReportConnectorOutOfOrder fault => $"ReportConnectorOutOfOrder {fault.ErrorCode}",
        SuspendCharging suspend => $"SuspendCharging {suspend.By.GetType().Name}",
        _ => command.GetType().Name,
    };

    private static JsonElement Payload(OcppFrame reply) =>
        reply.Value.ShouldBeOfType<CallResult>().Payload;

    private static string? IdTagStatus(OcppFrame reply) =>
        Payload(reply).GetProperty("idTagInfo").GetProperty("status").GetString();
}
