using Akka.Actor;
using Akka.Hosting;
using Akka.Serialization;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Tariffs.Actors.Serialization;
using Ohmline.Testing;

namespace Ohmline.Charging.Api.Tests;

/// <summary>
/// The test kit for Charging's edge: a probe for the region, a fake clock, a short timeout,
/// and every message serialized on its way to the probe, as it would be to another node.
/// </summary>
public abstract class EdgeTestKit(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Charging, Tariffs), output: output)
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected FakeTimeProvider Clock { get; } = new(Morning);

    /// <summary>
    /// Three seconds: long enough for a probe's answer on a machine running the whole suite
    /// (500 ms timed out now and then from Chapter 21's test count on), and only the test that
    /// waits for a 504 waits for it.
    /// </summary>
    protected static IOptions<ChargingApiOptions> Options { get; } =
        Microsoft.Extensions.Options.Options.Create(
            new ChargingApiOptions { AskTimeout = TimeSpan.FromSeconds(3) });

    protected static IRequiredActor<TKey> RegionOf<TKey>(TestProbe probe) =>
        new Region<TKey>(probe.Ref);

    /// <summary>Waits for what the endpoint asks, and answers it as the aggregate would.</summary>
    protected static async Task<TMessage> AnswerAsync<TMessage>(TestProbe region, object reply)
    {
        var asked = await region.ExpectMsgAsync<TMessage>(cancellationToken: Ct);
        region.Reply(reply);
        return asked;
    }

    protected static ProblemHttpResult Problem(IResult result, int status)
    {
        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(status);
        return problem;
    }

    private static SerializerDetails Charging(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            ChargingSerializer.Name,
            new ChargingSerializer(system),
            [.. ChargingSerializer.BoundTypes]);

    private static SerializerDetails Tariffs(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            TariffsSerializer.Name,
            new TariffsSerializer(system),
            [.. TariffsSerializer.BoundTypes]);
}
