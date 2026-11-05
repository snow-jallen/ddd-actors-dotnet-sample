using Akka.Actor;
using Akka.TestKit;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Actors.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using static Ohmline.Charging.Actors.Tests.Journeys.TheJourney;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The four contexts a journey talks to, as test probes with autopilots (section 15.4): Driver
/// Accounts, Site Energy, the payment side and the charge point. Each answers as that context
/// would, and keeps what it was asked for the test.
/// </summary>
internal sealed record FakeContexts(
    TestProbe Tokens, TestProbe Budgets, TestProbe Payments, TestProbe Chargers)
{
    /// <summary>
    /// Contexts that agree to everything, except that the payment side answers as the test says.
    /// </summary>
    public static FakeContexts SayingYes(
        TestKitBase kit, Func<PortCall, PaymentPortResult> payments)
    {
        var contexts = new FakeContexts(
            kit.CreateTestProbe("driver-accounts"),
            kit.CreateTestProbe("site-energy"),
            kit.CreateTestProbe("payments"),
            kit.CreateTestProbe("charge-points"));
        AnswerWith(contexts.Tokens, message => message switch
        {
            LockForSession lockFor =>
                new TokenLocked(lockFor.TokenUid, lockFor.SessionId, Driver, Version: 2),
            _ => new TokenCommandAccepted(Version: 3),
        });
        AnswerWith(contexts.Budgets, message => message switch
        {
            RequestAllocation request => new PowerAllocated(
                request.SiteId,
                request.ConnectorId,
                request.SessionId,
                request.Requested,
                Version: 7),
            _ => new BudgetCommandAccepted(Version: 8),
        });
        AnswerWith(contexts.Payments, message => payments((PortCall)message));
        AnswerWith(contexts.Chargers, message => new ChargerAccepted(
            ((PortCall.RemoteStart)message).ConnectorId));
        return contexts;
    }

    /// <summary>
    /// A payment side that answers a hold so, and takes or voids whatever it is asked.
    /// </summary>
    public static Func<PortCall, PaymentPortResult> PlaceHoldThen(PaymentPortResult hold) =>
        call => call switch
        {
            PortCall.PlaceHold => hold,
            PortCall.Capture capture => new PaymentCaptured(capture.HoldId, capture.Amount),
            PortCall.Void @void => new HoldVoided(@void.Hold.Value),
            _ => throw new InvalidOperationException($"The payment port has no {call}."),
        };

    /// <summary>The journey, among these contexts, with the test's instruments.</summary>
    public Props Journey(
        JourneyId journeyId, TimeProvider clock, ChargingMetrics? metrics = null) =>
        ChargingJourneyActor.Props(
            journeyId,
            clock,
            new Region<IdTokenRegion>(Tokens.Ref),
            new Region<SitePowerBudgetRegion>(Budgets.Ref),
            new ProbePaymentPort(Payments.Ref),
            new ProbeChargerPort(Chargers.Ref),
            metrics);

    /// <summary>The probe answers every message it gets, and keeps it for the test.</summary>
    private static void AnswerWith(TestProbe probe, Func<object, object> answer) =>
        probe.SetAutoPilot(new DelegateAutoPilot((sender, message) =>
        {
            sender.Tell(answer(message));
            return AutoPilot.KeepRunning;
        }));
}
