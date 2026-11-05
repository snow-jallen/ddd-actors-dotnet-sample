using Akka.TestKit.Xunit;
using Microsoft.Data.Sqlite;
using Ohmline.ActorShell.Tracing;
using Ohmline.Testing;

namespace Ohmline.JournalInspect.Tests;

/// <summary>
/// The tool against a SQLite journal holding what the node writes: a plain event, and one in a
/// trace envelope as events are stored from section 17.7.
/// </summary>
public sealed class JournalReaderTests(ITestOutputHelper output)
    : TestKit(
        ActorTestConfig.With(Payloads.Charging, Payloads.Envelopes),
        nameof(JournalReaderTests),
        output)
{
    private const string ChargePoint = "charge-point-OHM-SLC-0042";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_an_aggregate_s_events_then_each_row_says_who_can_read_it()
    {
        await using var journal = await SqliteJournal.CreateAsync(Ct);
        await journal.AddEventAsync(
            ChargePoint, 1, Payloads.Write(Sys, Payloads.Reservation), Ct);
        await journal.AddEventAsync(
            ChargePoint,
            2,
            Payloads.Write(
                Sys, new TraceEnvelope(Payloads.Reservation, Payloads.TraceParent, null)),
            Ct);

        var lines = await ReadAsync(journal, ["--persistence-id", ChargePoint]);

        lines.Count.ShouldBe(2);
        lines[0].ShouldStartWith(
            "ordering=1 | persistence_id=charge-point-OHM-SLC-0042 | sequence_nr=1 | " +
            "created=2026-11-04T14:40:00.0000000+00:00 | serializer_id=9101 | " +
            "manifest=charging.connector-reserved.v1 | payload={\"tokenUid\":");
        lines[1].ShouldStartWith(
            "ordering=2 | persistence_id=charge-point-OHM-SLC-0042 | sequence_nr=2 | " +
            "created=2026-11-04T14:40:00.0000000+00:00 | serializer_id=9100>9101 | " +
            "manifest=ohmline.trace-envelope.v1>charging.connector-reserved.v1 | " +
            $"traceparent={Payloads.TraceParent} | payload={{\"tokenUid\":");
    }

    [Fact]
    public async Task Given_no_persistence_id_then_every_aggregate_is_read_in_journal_order()
    {
        await using var journal = await SqliteJournal.CreateAsync(Ct);
        var reservation = Payloads.Write(Sys, Payloads.Reservation);
        await journal.AddEventAsync(ChargePoint, 1, reservation, Ct);
        await journal.AddEventAsync("charge-point-OHM-SLC-0043", 1, reservation, Ct);
        await journal.AddEventAsync(ChargePoint, 2, reservation, Ct);

        var lines = await ReadAsync(journal, ["--after", "1", "--limit", "5"]);

        lines.Select(line => line.Split(" | ")[..3]).ShouldBe(
        [
            ["ordering=2", "persistence_id=charge-point-OHM-SLC-0043", "sequence_nr=1"],
            ["ordering=3", "persistence_id=charge-point-OHM-SLC-0042", "sequence_nr=2"],
        ]);
    }

    [Fact]
    public async Task Given_snapshots_asked_for_then_the_snapshot_rows_are_read()
    {
        await using var journal = await SqliteJournal.CreateAsync(Ct);
        await journal.AddSnapshotAsync(
            ChargePoint, 100, (9101, "charging.charge-point-snapshot.v1", "{}"u8.ToArray()), Ct);

        var lines = await ReadAsync(journal, ["--persistence-id", ChargePoint, "--snapshots"]);

        lines.ShouldBe(
        [
            "persistence_id=charge-point-OHM-SLC-0042 | sequence_nr=100 | " +
            "created=2026-11-04T14:40:00.0000000+00:00 | serializer_id=9101 | " +
            "manifest=charging.charge-point-snapshot.v1 | payload={}",
        ]);
    }

    [Fact]
    public void Given_no_database_or_two_then_there_are_no_options()
    {
        InspectOptions.Parse(["--persistence-id", ChargePoint]).ShouldBeNull();
        InspectOptions.Parse(["--sqlite", "a.db", "--postgres", "Host=localhost"]).ShouldBeNull();
    }

    private static async Task<IReadOnlyList<string>> ReadAsync(
        SqliteJournal journal, string[] args)
    {
        var options = InspectOptions.Parse(["--sqlite", journal.Path, .. args]).ShouldNotBeNull();
        await using var connection = options.Connect();
        connection.ShouldBeOfType<SqliteConnection>();
        await connection.OpenAsync(Ct);
        var rows = await options.ReadAsync(new JournalReader(connection), Ct);
        return [.. rows.Select(RowFormat.Line)];
    }
}
