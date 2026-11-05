using System.Collections.Immutable;
using System.Globalization;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Serialization;

namespace Ohmline.Testing;

/// <summary>
/// The configuration every actor test in Ohmline runs with. Every message is serialized and
/// deserialized on its way to an actor, as it would be between two cluster nodes, so a message
/// that cannot make the trip (a union, a type without a manifest) fails here and not first in
/// production. Props are not serialized: an actor's constructor may take a clock.
/// </summary>
public static class ActorTestConfig
{
    /// <summary>Scales every TestKit timeout: 1 by default, 3 on CI.</summary>
    public const string TimeFactorVariable = "OHMLINE_TIMEFACTOR";

    public static Config Config { get; } = ConfigurationFactory.ParseString(
        $"""
        akka.actor.serialize-messages = on
        akka.actor.serialize-creators = off
        akka.test.timefactor = {TimeFactor.ToString(CultureInfo.InvariantCulture)}
        """);

    public static double TimeFactor =>
        double.TryParse(
            Environment.GetEnvironmentVariable(TimeFactorVariable),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var factor) && factor > 0
            ? factor
            : 1.0;

    /// <summary>
    /// The configuration plus the serializers a context registers in production, for a TestKit
    /// constructor that takes an <see cref="ActorSystemSetup"/>.
    /// </summary>
    public static ActorSystemSetup With(
        params IEnumerable<Func<ExtendedActorSystem, SerializerDetails>> serializers)
    {
        ExtendedActorSystem? started = null;
        return BootstrapSetup.Create()
            .WithConfig(Config)
            .And(SerializationSetup.Create(system =>
            {
                started = system;
                return serializers.Select(serializer => serializer(system)).ToImmutableHashSet();
            }))
            .And(NewtonSoftJsonSerializerSetup.Create(settings =>
                settings.Converters.Add(new BoundTypeJsonConverter(() => started))));
    }
}
