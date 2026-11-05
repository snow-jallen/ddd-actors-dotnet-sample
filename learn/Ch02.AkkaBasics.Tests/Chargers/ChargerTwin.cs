using Akka.Actor;

namespace Ch02.AkkaBasics.Tests.Chargers;

/// <summary>
/// The back office's picture of one physical charge point. Until the charger's
/// BootNotification arrives the twin knows nothing, so it stashes every other message. After
/// that its behaviour is its state: Become swaps the set of handlers as the charger reports a
/// new status, and each behaviour answers GetStatus with its own status. A periodic timer
/// counts missed heartbeats and marks the charger offline after three.
/// </summary>
public sealed class ChargerTwin : ReceiveActor, IWithStash, IWithTimers
{
    public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromMinutes(1);

    private const string HeartbeatCheckKey = "heartbeat-check";
    private const int MaxMissedHeartbeats = 3;

    private readonly string _chargePointId;
    private readonly TimeSpan _heartbeatInterval;
    private string _firmwareVersion = "";
    private int _missedHeartbeats;

    public ChargerTwin(string chargePointId, TimeSpan heartbeatInterval)
    {
        _chargePointId = chargePointId;
        _heartbeatInterval = heartbeatInterval;
        Booting();
    }

    /// <summary>Set by Akka.NET when it creates the actor.</summary>
    public IStash Stash { get; set; } = null!;

    /// <summary>Set by Akka.NET. Its timers are cancelled when the actor stops.</summary>
    public ITimerScheduler Timers { get; set; } = null!;

    public static Props Props(string chargePointId, TimeSpan? heartbeatInterval = null)
    {
        var interval = heartbeatInterval ?? DefaultHeartbeatInterval;
        return Akka.Actor.Props.Create(() => new ChargerTwin(chargePointId, interval));
    }

    private void Booting()
    {
        Receive<BootNotification>(boot =>
        {
            Start(boot);
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
        Receive<BootNotification>(Start);

        Receive<Heartbeat>(_ => _missedHeartbeats = 0);
        Receive<CheckHeartbeat>(_ =>
        {
            if (++_missedHeartbeats < MaxMissedHeartbeats)
                return;

            Timers.Cancel(HeartbeatCheckKey);
            Context.Parent.Tell(new ChargerWentOffline(_chargePointId, _missedHeartbeats));
            Become(Offline);
        });
    }

    private void Offline()
    {
        // The twin no longer knows the charger's status. Only a new BootNotification, which a
        // charger sends when it reconnects, brings it back.
        Receive<BootNotification>(Start);
        Receive<GetStatus>(_ => Sender.Tell(
            new ChargerStatusReport(_chargePointId, ChargerStatus.Offline, _firmwareVersion)));
    }

    private void Start(BootNotification boot)
    {
        _firmwareVersion = boot.FirmwareVersion;
        _missedHeartbeats = 0;
        // Starting a timer with a key that is already in use replaces the old timer.
        Timers.StartPeriodicTimer(HeartbeatCheckKey, CheckHeartbeat.Instance, _heartbeatInterval);
        Become(Available);
    }

    private sealed record CheckHeartbeat
    {
        public static readonly CheckHeartbeat Instance = new();
    }
}
