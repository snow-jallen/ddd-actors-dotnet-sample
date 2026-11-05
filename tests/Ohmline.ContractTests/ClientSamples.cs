using System.Collections.Immutable;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Notifications;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Charging.Client.Contracts.Requests;

namespace Ohmline.ContractTests;

/// <summary>
/// One of every client DTO, with every field filled in: a driver at the depot on 2026-11-04
/// starts charging on <c>OHM-SLC-0042/1</c> from the app, and watches the session's card.
/// </summary>
internal static class ClientSamples
{
    public static readonly Guid Command = Guid.Parse("019a4f6e-1c40-7c11-9a2b-3c4d00000c01");

    public static readonly Guid Journey = Guid.Parse("019a4f6e-1c40-7d22-8b3c-4d5e00000d01");

    public static readonly DateTimeOffset Started = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static DriverSessionCard Card { get; } = new(
        Journey,
        "04A2B3C4D5E6F7",
        "charging",
        "OHM-SLC-0042/1",
        "Salt Lake bus depot, public bays",
        7.35m,
        Started,
        EndedAt: null,
        Version: 2);

    /// <summary>Every client DTO, by the name of its golden file in tests/golden/client.</summary>
    public static ImmutableDictionary<string, object> Dtos { get; } =
        new Dictionary<string, object>
        {
            ["start-charging-request"] = new StartChargingRequest(
                Command, Journey, "OHM-SLC-0042/1", "04A2B3C4D5E6F7", 40m, "USD"),
            ["stop-charging-request"] = new StopChargingRequest(Command),
            ["reserve-connector-request"] = new ReserveConnectorRequest(Command, "04A2B3C4D5E6F7"),
            ["command-accepted-dto"] = new CommandAcceptedDto(Command, 1),
            ["problem-dto"] = new ProblemDto(
                "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                "The command was rejected.",
                409,
                "Connector 1 is reserved for another token until 2026-11-04T14:55:00+00:00.",
                RejectionCodes.ReservedForAnotherToken),
            ["driver-session-card"] = Card,
            ["driver-session-cards"] = new DriverSessionCards(Card, [], Position: 2),
            ["driver-feed-entry"] = new DriverFeedEntry(2, Card),
            ["driver-feed-page"] = new DriverFeedPage([new DriverFeedEntry(2, Card)], 2),
            ["charge-point-listing-dto"] = new ChargePointListingDto(
                "OHM-SLC-0042",
                3,
                "Salt Lake bus depot, public bays",
                "Enter from 400 South; the bays are on the left.",
                "Mo-Su 05:00-23:00"),
            ["session-summary-changed"] = new SessionSummaryChanged(Journey, 2, "charging", 7.35m),
            ["driver-card-changed"] = new DriverCardChanged(
                2, Card, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"),
        }.ToImmutableDictionary();
}
