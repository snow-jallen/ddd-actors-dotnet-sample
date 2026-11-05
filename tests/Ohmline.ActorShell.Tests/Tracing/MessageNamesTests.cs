using Ohmline.ActorShell.Tests.Messaging;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Tests.Tracing;

public sealed class MessageNamesTests
{
    [Fact]
    public void Given_a_record_then_its_name_is_its_type_name() =>
        MessageNames.Of(new Ping("hello")).ShouldBe("Ping");

    [Fact]
    public void Given_a_union_sent_by_mistake_then_its_name_is_the_case_it_holds() =>
        MessageNames.Of(new Signal(new Pong(3))).ShouldBe("Pong");

    [Fact]
    public void Given_a_default_union_then_its_name_is_the_unions_own() =>
        MessageNames.Of(default(Signal)).ShouldBe("Signal");
}
