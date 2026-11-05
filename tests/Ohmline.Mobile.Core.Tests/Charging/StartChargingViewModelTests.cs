using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Outbox;
using Ohmline.Mobile.Core.Tests.Outbox;

namespace Ohmline.Mobile.Core.Tests.Charging;

public sealed class StartChargingViewModelTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    private readonly TemporaryDatabase _database = new();
    private readonly FakeSender _server = new();
    private readonly FakeConnectivity _network = new();
    private readonly SqliteOutbox _outbox;
    private readonly OutboxDispatcher _dispatcher;
    private readonly StartChargingViewModel _screen;

    public StartChargingViewModelTests()
    {
        var clock = new FakeTimeProvider(Morning);
        _outbox = new SqliteOutbox(_database.ConnectionString);
        _dispatcher = new OutboxDispatcher(
            _outbox, _server, _network, new InlineDispatcher(), clock);
        _screen = new StartChargingViewModel(
            _outbox, _dispatcher, new DriverToken("04A2B3C4D5E6F7"), _network, clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_input_that_is_not_valid_then_each_field_says_why_and_nothing_is_kept()
    {
        _screen.ConnectorText = "OHM-SLC-0042";
        _screen.SpendCapText = "0";

        await _screen.StartChargingCommand.ExecuteAsync(null);

        _screen.ConnectorError.ShouldBe(
            "'OHM-SLC-0042' is not a connector code. It looks like OHM-SLC-0042/1.");
        _screen.SpendCapError.ShouldBe("A spend cap is from 1 to 500 USD, not 0.");
        _screen.Status.ShouldBeNull();
        (await _outbox.ReadQueuedAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_valid_tap_then_the_command_is_queued_with_ids_made_at_the_tap()
    {
        _screen.ConnectorText = "ohm-slc-0042/1";

        await _screen.StartChargingCommand.ExecuteAsync(null);

        _screen.Status.ShouldNotBeNull().Value.ShouldBe(new Queued(0));
        var row = (await _outbox.ReadQueuedAsync(Ct)).ShouldHaveSingleItem();
        var request = JsonSerializer.Deserialize(
            row.Payload, ChargingClientJsonContext.Default.StartChargingRequest)!;
        request.ShouldBe(request with
        {
            ConnectorId = "OHM-SLC-0042/1",
            TokenUid = "04A2B3C4D5E6F7",
            SpendCapAmount = 40m,
            Currency = "USD",
        });
        (request.CommandId.Version, request.JourneyId.Version).ShouldBe((7, 7));
        (row.CommandId, row.AggregateKey)
            .ShouldBe((request.CommandId, request.JourneyId.ToString()));
    }

    [Fact]
    public async Task Given_a_double_tap_then_one_command_is_queued()
    {
        _screen.ConnectorText = "OHM-SLC-0042/1";

        await _screen.StartChargingCommand.ExecuteAsync(null);
        await _screen.StartChargingCommand.ExecuteAsync(null);

        (await _outbox.ReadQueuedAsync(Ct)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Given_no_signal_then_the_command_waits_and_the_screen_says_offline()
    {
        _network.IsOnline = false;
        _screen.ConnectorText = "OHM-SLC-0042/1";

        await _screen.StartChargingCommand.ExecuteAsync(null);

        _screen.Screen.Value.ShouldBeOfType<Offline>();
        _screen.Status.ShouldNotBeNull().Value.ShouldBe(new Queued(0));
    }

    [Fact]
    public async Task Given_a_rejection_then_it_reads_as_a_sentence_and_the_next_tap_is_new()
    {
        _server.Then(new SendRejected("reserved-for-another-token", "Connector 1 is reserved."));
        _screen.ConnectorText = "OHM-SLC-0042/1";
        await _screen.StartChargingCommand.ExecuteAsync(null);
        _dispatcher.Start();

        await _dispatcher.FlushAsync();
        await _dispatcher.StopAsync(); // So that the next tap's command stays queued.
        await _screen.StartChargingCommand.ExecuteAsync(null);

        _screen.Status.ShouldNotBeNull().Value.ShouldBe(new Queued(0));
        var rows = await _outbox.ReadForAsync(_server.Sent[0].AggregateKey, Ct);
        rows.ShouldHaveSingleItem().ToStatus().Value.ShouldBe(new Rejected(
            "reserved-for-another-token", "This connector is reserved for another driver."));
        (await _outbox.ReadQueuedAsync(Ct)).ShouldHaveSingleItem().CommandId
            .ShouldNotBe(_server.Sent[0].CommandId);
    }

    [Fact]
    public async Task Given_the_server_accepts_then_the_screen_hears_it()
    {
        _server.Then(new SendAccepted(1));
        _screen.ConnectorText = "OHM-SLC-0042/1";
        await _screen.StartChargingCommand.ExecuteAsync(null);
        _dispatcher.Start();

        await _dispatcher.FlushAsync();

        _screen.Status.ShouldNotBeNull().Value.ShouldBe(new Accepted(1));
    }

    public async ValueTask DisposeAsync()
    {
        await _dispatcher.StopAsync();
        _database.Dispose();
    }
}
