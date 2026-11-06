using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Serialization;

namespace Ch21.SerializationV2.Tests;

/// <summary>Akka.Serialization.V2 and a C# 15 union field (section 21.10).</summary>
public sealed class UnionFieldTests : IAsyncLifetime
{
    private readonly ActorSystem _system = ActorSystem.Create(
        "v2",
        ActorSystemSetup.Create(SerializationSetup.Create(system =>
            [AuthorizationSerializer.CreateSerializerDetails(system)])));

    public static TheoryData<string> Cases => ["rfid card", "app token"];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Given_either_case_of_the_union_then_the_message_round_trips(string how)
    {
        var authorized = new ChargingAuthorized(
            "019a4f6e-1c40-7a51-8e2f-3b1d00000001",
            how == "rfid card"
                ? new RfidCard("04A2B3C4D5E6F7")
                : new AppToken("drv-7f3a", "pixel-8"));

        var back = RoundTrip(authorized);

        back.ShouldBe(authorized);
        back.By.Value.ShouldBe(authorized.By.Value);
    }

    [Fact]
    public void The_generated_serializer_writes_its_own_id_and_the_message_s_manifest()
    {
        var authorized = new ChargingAuthorized("s-1", new RfidCard("04A2B3C4D5E6F7"));

        var serializer = _system.Serialization.FindSerializerFor(authorized);

        serializer.Identifier.ShouldBe(9199);
        Serialization.ManifestFor(serializer, authorized).ShouldBe("learn.charging-authorized.v1");
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() =>
        await _system.Terminate().WaitAsync(TestContext.Current.CancellationToken);

    private ChargingAuthorized RoundTrip(ChargingAuthorized message)
    {
        var serializer = _system.Serialization.FindSerializerFor(message);
        var bytes = serializer.ToBinary(message);
        var manifest = Serialization.ManifestFor(serializer, message);
        return _system.Serialization.Deserialize(bytes, serializer.Identifier, manifest)
            .ShouldBeOfType<ChargingAuthorized>();
    }
}
