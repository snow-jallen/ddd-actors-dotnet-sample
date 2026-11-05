using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SiteEnergy.Contracts;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// Everything a charging journey reacts to, from wherever it comes: the edge's command, Driver
/// Accounts' and Site Energy's replies in their own published words, the results of the
/// journey's own ports, the events of its session, and its timers. These records share no base
/// type and live in four projects; a union makes them one closed set, so the journey's decision
/// table must say what each does in each state.
/// </summary>
public union JourneyInput(
    StartCharging,
    TokenLocked,
    TokenLockRefused,
    HoldPlaced,
    HoldDeclined,
    HoldVoided,
    PowerAllocated,
    QueuedForPower,
    BudgetCommandRejected,
    ChargerAccepted,
    ChargerRejected,
    SessionStarted,
    SessionEnded,
    PaymentCaptured,
    CaptureFailed,
    JourneyTimerElapsed);
