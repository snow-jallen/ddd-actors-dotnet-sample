using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Messaging;

namespace Ohmline.ActorShell.Tests.Messaging;

/// <summary>
/// Every message goes through the serializer here, as it would between cluster nodes, so a
/// union that reaches the wire shows up as one that arrives empty.
/// </summary>
public sealed class SendExtensionsTests(ITestOutputHelper output)
    : TestKit("akka.actor.serialize-messages = on", output: output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_union_when_sent_then_the_recipient_receives_its_case()
    {
        Signal signal = new Ping("anyone there?");

        TestActor.Send(signal);

        await ExpectMsgAsync(new Ping("anyone there?"), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_union_when_sent_from_a_sender_then_the_recipient_sees_that_sender()
    {
        var sender = CreateTestProbe("sender");
        Signal signal = new Pong(3);

        TestActor.Send(signal, sender.Ref);

        await ExpectMsgAsync(new Pong(3), cancellationToken: Ct);
        LastSender.ShouldBe(sender.Ref);
    }

    [Fact]
    public void Given_a_default_union_when_sent_then_it_throws_before_anything_is_told()
    {
        Should.Throw<ArgumentException>(() => TestActor.Send(default(Signal)))
            .Message.ShouldStartWith("This Signal is default");
    }

    [Fact]
    public async Task Given_a_union_when_told_instead_of_sent_then_it_arrives_empty()
    {
        Signal signal = new Ping("anyone there?");

        TestActor.Tell(signal);

        (await ExpectMsgAsync<Signal>(cancellationToken: Ct)).Value.ShouldBeNull();
    }
}
