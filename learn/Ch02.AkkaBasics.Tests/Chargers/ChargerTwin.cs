using Akka.Actor;

namespace Ch02.AkkaBasics.Tests.Chargers;

/// <summary>
/// The back office's picture of one physical charge point. Until the charger's
/// BootNotification arrives the twin knows nothing, so it stashes every other message. After
/// that its behaviour is its state: Become swaps the set of handlers as the charger reports a
/// new status, and each behaviour answers GetStatus with its own status.
/// </summary>
public sealed class ChargerTwin : ReceiveActor, IWithStash
{
    private readonly string _chargePointId;
    private string _firmwareVersion = "";

    public ChargerTwin(string chargePointId)
    {
        _chargePointId = chargePointId;
        Booting();
    }

    /// <summary>Set by Akka.NET when it creates the actor.</summary>
    public IStash Stash { get; set; } = null!;

    public static Props Props(string chargePointId) =>
        Akka.Actor.Props.Create(() => new ChargerTwin(chargePointId));

    private void Booting()
    {
        Receive<BootNotification>(boot =>
        {
            _firmwareVersion = boot.FirmwareVersion;
            Become(Available);
            Stash.UnstashAll(); // The stashed messages go back to the front of the mailbox.
        });
        ReceiveAny(_ => Stash.Stash());
    }

    private void Available()
    {
        Receive<StatusChanged>(s => s.Status == ChargerStatus.Charging, _ => Become(Charging));
        Receive<StatusChanged>(s => s.Status == ChargerStatus.Faulted, _ => Become(Faulted));
        ReceiveWhileRunning(ChargerStatus.Available);
    }

    private void Charging()
    {
        Receive<StatusChanged>(s => s.Status == ChargerStatus.Available, _ => Become(Available));
        Receive<StatusChanged>(s => s.Status == ChargerStatus.Faulted, _ => Become(Faulted));
        ReceiveWhileRunning(ChargerStatus.Charging);
    }

    private void Faulted()
    {
        // A faulted charger must be repaired (report Available) before it can charge again, so
        // StatusChanged(Charging) has no handler here and goes unhandled.
        Receive<StatusChanged>(s => s.Status == ChargerStatus.Available, _ => Become(Available));
        ReceiveWhileRunning(ChargerStatus.Faulted);
    }

    private void ReceiveWhileRunning(ChargerStatus status)
    {
        Receive<GetStatus>(_ =>
            Sender.Tell(new ChargerStatusReport(_chargePointId, status, _firmwareVersion)));

        // The charger restarted: it comes back Available, perhaps with new firmware.
        Receive<BootNotification>(boot =>
        {
            _firmwareVersion = boot.FirmwareVersion;
            Become(Available);
        });
    }
}
