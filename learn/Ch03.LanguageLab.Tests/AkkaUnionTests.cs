using System.Text;
using Akka.Actor;
using Akka.Serialization;
using Akka.TestKit.Xunit;
using Ch03.ClosedLib;

namespace Ch03.LanguageLab.Tests;

public sealed class AkkaUnionTests(ITestOutputHelper output) : TestKit(output: output)
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task Given_a_union_when_told_then_it_arrives_as_the_union_not_its_case()
    {
        var actor = Sys.ActorOf(HandlerReportingActor.Props());
        ConnectorStatus status = new Occupied("session-a");

        actor.Tell(status, TestActor);

        await ExpectMsgAsync(
            "Receive<ConnectorStatus>: occupied by session-a",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_the_case_of_a_union_when_told_then_the_case_handler_receives_it()
    {
        var actor = Sys.ActorOf(HandlerReportingActor.Props());
        ConnectorStatus status = new Occupied("session-a");

        actor.Tell(status.Value!, TestActor);

        await ExpectMsgAsync(
            "Receive<Occupied>: session-a",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_a_closed_record_leaf_when_told_then_the_base_type_handler_receives_it()
    {
        var actor = Sys.ActorOf(HandlerReportingActor.Props());

        actor.Tell(new RemoteStop("OHM-SLC-0042", "session-a"), TestActor);

        await ExpectMsgAsync(
            "Receive<ChargerCommand>: RemoteStop",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Given_the_default_serializer_when_a_union_round_trips_then_its_case_is_lost()
    {
        ConnectorStatus status = new Occupied("session-a");
        var serializer = Sys.Serialization.FindSerializerFor(status);

        var bytes = serializer.ToBinary(status);
        var restored = serializer.FromBinary(bytes, typeof(ConnectorStatus));

        serializer.ShouldBeOfType<NewtonSoftJsonSerializer>();
        Encoding.UTF8.GetString(bytes).ShouldContain("\"SessionId\":\"session-a\"");
        restored.ShouldBeOfType<ConnectorStatus>().Value.ShouldBeNull();
    }

    [Fact]
    public void Given_the_default_serializer_when_a_closed_record_round_trips_then_it_survives()
    {
        ChargerCommand command = new RemoteStop("OHM-SLC-0042", "session-a");
        var serializer = Sys.Serialization.FindSerializerFor(command);

        var restored = serializer.FromBinary(serializer.ToBinary(command), typeof(RemoteStop));

        restored.ShouldBe(command);
    }

    [Fact]
    public async Task Given_serialize_messages_when_a_union_is_told_then_the_actor_gets_it_empty()
    {
        // Every actor test in the sample turns this on (section 10.4): each message goes through
        // the serializer, as it would between cluster nodes.
        var system = ActorSystem.Create("serialize-messages", "akka.actor.serialize-messages = on");
        try
        {
            var actor = system.ActorOf(HandlerReportingActor.Props());
            ConnectorStatus status = new Occupied("session-a");

            var reply = await actor.Ask<string>(
                status, AskTimeout, TestContext.Current.CancellationToken);

            reply.ShouldBe("Receive<ConnectorStatus>: empty");
        }
        finally
        {
            await system.Terminate();
        }
    }
}
