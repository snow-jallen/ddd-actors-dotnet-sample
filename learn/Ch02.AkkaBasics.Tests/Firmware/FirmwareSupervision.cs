using Akka.Actor;
using Akka.Pattern;

namespace Ch02.AkkaBasics.Tests.Firmware;

public static class FirmwareSupervision
{
    /// <summary>
    /// Wraps the lookup actor in a BackoffSupervisor. When the decider says Restart, the child
    /// is started again after minBackoff, then twice as long after each further failure, up to
    /// maxBackoff. Once a child has run for resetAfter, the next failure starts from minBackoff.
    /// </summary>
    public static Props WithBackoff(
        Props lookup, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan resetAfter) =>
        BackoffSupervisor.Props(
            Backoff.OnFailure(
                    lookup,
                    childName: "firmware-lookup",
                    minBackoff: minBackoff,
                    maxBackoff: maxBackoff,
                    randomFactor: 0.2,
                    maxNrOfRetries: -1)
                .WithAutoReset(resetAfter)
                .WithSupervisorStrategy(new OneForOneStrategy(Decide)));

    /// <summary>What to do with a child that threw, decided by the exception.</summary>
    public static Directive Decide(Exception exception) => exception switch
    {
        ArgumentException => Directive.Resume, // A bad request: drop it, keep the cache.
        FirmwareCatalogUnavailableException => Directive.Restart, // Try again, with backoff.
        _ => Directive.Escalate,
    };
}
