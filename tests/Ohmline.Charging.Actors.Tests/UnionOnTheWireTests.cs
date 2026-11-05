using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// WRONG ON PURPOSE (branch wrong/ch10-s02-union-on-the-wire). This shell replies with the
/// decider's error union as it is, instead of mapping it to a CommandRejected. Every message is
/// serialized here, as between cluster nodes, and the reply arrives as an empty union.
/// </summary>
public sealed class UnionOnTheWireTests(ITestOutputHelper output)
    : TestKit("akka.actor.serialize-messages = on", output: output)
{
    private static readonly ChargePointId ChargePoint = new("OHM-SLC-0042");

    [Fact]
    public async Task Demonstrates_an_error_union_replied_as_it_is_arrives_empty()
    {
        var shell = Sys.ActorOf(Props.Create(() => new UnionReplyingShell()));
        var command = new ReserveConnector(
            new CommandId(Guid.CreateVersion7()), ChargePoint, 1, new TokenUid("04A2B3C4"));

        shell.Tell(command, TestActor);

        var reply = await ExpectMsgAsync<ChargePointError>(
            cancellationToken: TestContext.Current.CancellationToken);
        reply.Value.ShouldBeNull(); // The reason, the connector and the expiry are all gone.
    }

    private sealed class UnionReplyingShell : ReceiveActor
    {
        public UnionReplyingShell() =>
            Receive<ReserveConnector>(reserve =>
            {
                var until = new DateTimeOffset(2026, 11, 4, 7, 55, 0, TimeSpan.Zero);
                ChargePointError refusal =
                    new ChargePointError.ReservedForAnotherToken(reserve.ConnectorNumber, until);
                Sender.Tell(refusal); // The mistake: should be Sender.Tell(refusal.ToReply()).
            });
    }
}
