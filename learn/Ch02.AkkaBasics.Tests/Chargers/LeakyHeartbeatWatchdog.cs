#if DEMO_COMPILE_ERRORS
using Akka.Actor;

namespace Ch02.AkkaBasics.Tests.Chargers;

// Compile with -p:DefineConstants=DEMO_COMPILE_ERRORS to see Akka.Analyzers flag this (AK1004).
// Nothing cancels the repeated message when the actor stops or restarts; IWithTimers does.
public sealed class LeakyHeartbeatWatchdog : ReceiveActor
{
    protected override void PreStart() =>
        Context.System.Scheduler.ScheduleTellRepeatedly(
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), Self, new Heartbeat(), Self);
}
#endif
