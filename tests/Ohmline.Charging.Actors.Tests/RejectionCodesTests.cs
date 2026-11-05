using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// The driver app reads a rejection by its code (section 19.2). Every code the aggregates can
/// answer the app's commands with must be in the published list, or a driver sees the
/// fallback sentence for a refusal Ohmline knew all about.
/// </summary>
public sealed class RejectionCodesTests
{
    private static readonly ChargePointId ChargePoint = new("OHM-SLC-0042");

    private static readonly SessionId Session =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    /// <summary>Every reason a charge point can refuse a reservation with.</summary>
    private static readonly ChargePointError[] ReservationRefusals =
    [
        new ChargePointError.NotRegistered(ChargePoint),
        new ChargePointError.UnknownConnector(3),
        new ChargePointError.ConnectorUnavailable(1),
        new ChargePointError.ReservedForAnotherToken(1, DateTimeOffset.UnixEpoch),
        new ChargePointError.ConnectorOutOfOrder(2, "GroundFailure"),
    ];

    /// <summary>Every reason a session can refuse to stop with.</summary>
    private static readonly SessionError[] StopRefusals =
    [
        new SessionError.NotStarted(Session),
        new SessionError.AlreadyEnded(Session),
    ];

    [Fact]
    public void Every_code_a_reservation_can_be_refused_with_is_published()
    {
        ReservationRefusals.Select(error => error.Code)
            .ShouldBeSubsetOf(RejectionCodes.All);
    }

    [Fact]
    public void Every_code_a_stop_can_be_refused_with_is_published()
    {
        StopRefusals.Select(error => error.Code).ShouldBeSubsetOf(RejectionCodes.All);
    }

    [Fact]
    public void The_code_a_start_can_be_refused_with_is_published()
    {
        RejectionCodes.All.ShouldContain(JourneyRefusals.JourneyExists);
    }

    [Fact]
    public void The_published_codes_are_distinct()
    {
        RejectionCodes.All.ShouldBeUnique();
    }
}
