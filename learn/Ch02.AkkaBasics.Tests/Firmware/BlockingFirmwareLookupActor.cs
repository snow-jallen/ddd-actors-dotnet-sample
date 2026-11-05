using Akka.Actor;

namespace Ch02.AkkaBasics.Tests.Firmware;

/// <summary>
/// The same lookup with ReceiveAsync. Akka.NET suspends the mailbox until the awaited task
/// completes, so nothing else is processed meanwhile, not even a Ping. In exchange, the
/// handler is simple and Sender still refers to the original sender after the await.
/// </summary>
public sealed class BlockingFirmwareLookupActor : ReceiveActor
{
    public BlockingFirmwareLookupActor(FirmwareCatalogClient catalog)
    {
        ReceiveAsync<CheckFirmware>(async check =>
        {
            var release = await catalog.GetLatestAsync(check.Model);
            Sender.Tell(release);
        });
        Receive<Ping>(_ => Sender.Tell(new Pong()));
    }

    public static Props Props(FirmwareCatalogClient catalog) =>
        Akka.Actor.Props.Create(() => new BlockingFirmwareLookupActor(catalog));
}
