using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Tests.Messaging;

namespace Ohmline.ActorShell.Tests.Serialization;

public sealed class ManifestJsonSerializerTests(ITestOutputHelper output) : TestKit(output: output)
{
    private SignalSerializer Serializer =>
        new((ExtendedActorSystem)Sys, SignalSerializer.Manifests);

    [Fact]
    public void Given_a_registered_type_when_written_then_it_is_camel_case_json_under_its_manifest()
    {
        var ping = new Ping("anyone there?");

        Serializer.Manifest(ping).ShouldBe("test.ping.v1");
        Encoding.UTF8.GetString(Serializer.ToBinary(ping)).ShouldBe("""{"text":"anyone there?"}""");
    }

    [Fact]
    public void Given_the_bytes_and_the_manifest_when_read_then_the_message_comes_back()
    {
        var bytes = Serializer.ToBinary(new Pong(3));

        Serializer.FromBinary(bytes, "test.pong.v1").ShouldBe(new Pong(3));
    }

    [Fact]
    public void Given_a_type_without_a_manifest_when_written_then_it_is_refused()
    {
        Should.Throw<ArgumentException>(() => Serializer.ToBinary("a string"))
            .Message.ShouldStartWith("SignalSerializer has no manifest for System.String.");
    }

    [Fact]
    public void Given_an_unknown_manifest_when_read_then_it_throws_a_serialization_exception()
    {
        Should.Throw<SerializationException>(() =>
                Serializer.FromBinary("{}"u8.ToArray(), "test.pang.v1"))
            .Message.ShouldBe("SignalSerializer (9199) does not know the manifest 'test.pang.v1'.");
    }

    [Fact]
    public void Given_a_registry_that_uses_a_manifest_twice_when_built_then_it_is_refused()
    {
        var twice = new Dictionary<Type, string>
        {
            [typeof(Ping)] = "test.signal.v1",
            [typeof(Pong)] = "test.signal.v1",
        };

        Should.Throw<ArgumentException>(() =>
            new SignalSerializer((ExtendedActorSystem)Sys, twice));
    }

    [Fact]
    public void Given_json_with_a_field_this_version_does_not_know_when_read_then_it_is_ignored()
    {
        var bytes = """{"text":"anyone there?","priority":"high"}"""u8.ToArray();

        Serializer.FromBinary(bytes, "test.ping.v1").ShouldBe(new Ping("anyone there?"));
    }

    [Fact]
    public void Given_json_without_a_required_field_when_read_then_it_is_refused()
    {
        Should.Throw<JsonException>(() => Serializer.FromBinary("{}"u8.ToArray(), "test.ping.v1"));
        Should.Throw<JsonException>(() =>
            Serializer.FromBinary("""{"text":null}"""u8.ToArray(), "test.ping.v1"));
    }

    [Fact]
    public void Given_json_from_before_an_optional_field_existed_when_read_then_it_has_its_default()
    {
        var older = """{"text":"charging"}"""u8.ToArray();

        Serializer.FromBinary(older, "test.chime.v1").ShouldBe(new Chime("charging"));
        Serializer.FromBinary("""{"text":"charging","volume":3}"""u8.ToArray(), "test.chime.v1")
            .ShouldBe(new Chime("charging", 3));
    }

    [Fact]
    public void Given_a_nullable_field_without_a_default_when_missing_then_older_json_is_refused()
    {
        Should.Throw<JsonException>(() =>
                Serializer.FromBinary("""{"text":"charging"}"""u8.ToArray(), "test.gong.v1"))
            .Message.ShouldContain("volume");
    }
}
