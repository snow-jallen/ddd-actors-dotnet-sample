using Akka.Actor;

namespace Ch02.AkkaBasics.Tests.Firmware;

/// <summary>
/// Looks up the latest firmware for a charger model and caches it. The lookup is I/O, so the
/// actor does not await it: PipeTo turns the task's outcome into a message to Self, and the
/// Loading behaviour stashes other requests until it arrives. The mailbox keeps running.
/// </summary>
public sealed class FirmwareLookupActor : ReceiveActor, IWithStash
{
    private readonly FirmwareCatalogClient _catalog;
    private readonly Dictionary<string, FirmwareRelease> _latest = [];

    public FirmwareLookupActor(FirmwareCatalogClient catalog)
    {
        _catalog = catalog;
        Ready();
    }

    public IStash Stash { get; set; } = null!;

    public static Props Props(FirmwareCatalogClient catalog) =>
        Akka.Actor.Props.Create(() => new FirmwareLookupActor(catalog));

    private void Ready()
    {
        Receive<CheckFirmware>(check =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(check.Model, nameof(check.Model));
            if (_latest.TryGetValue(check.Model, out var cached))
            {
                Sender.Tell(cached);
                return;
            }

            var requester = Sender; // Sender changes with every message; capture it now.
            _catalog.GetLatestAsync(check.Model).PipeTo(
                Self,
                success: release => new LookupSucceeded(release, requester),
                failure: exception => new LookupFailed(check.Model, exception, requester));
            Become(Loading);
        });
        Receive<Ping>(_ => Sender.Tell(new Pong()));
    }

    private void Loading()
    {
        Receive<LookupSucceeded>(succeeded =>
        {
            _latest[succeeded.Release.Model] = succeeded.Release;
            succeeded.Requester.Tell(succeeded.Release);
            Become(Ready);
            Stash.UnstashAll();
        });
        Receive<LookupFailed>(failed =>
        {
            failed.Requester.Tell(new FirmwareUnavailable(failed.Model));
            throw new FirmwareCatalogUnavailableException(failed.Model, failed.Cause);
        });
        Receive<Ping>(_ => Sender.Tell(new Pong()));
        ReceiveAny(_ => Stash.Stash());
    }

    private sealed record LookupSucceeded(FirmwareRelease Release, IActorRef Requester);

    private sealed record LookupFailed(string Model, Exception Cause, IActorRef Requester);
}
