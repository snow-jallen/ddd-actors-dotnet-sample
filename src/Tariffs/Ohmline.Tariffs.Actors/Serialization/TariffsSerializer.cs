using System.Collections.Frozen;
using System.Text.Json;
using Akka.Actor;
using Ohmline.ActorShell.Serialization;
using Ohmline.Tariffs.Actors.Book;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;

namespace Ohmline.Tariffs.Actors.Serialization;

/// <summary>
/// Everything Tariffs tells, asks, replies or persists, as JSON under a permanent manifest. The
/// identifier and the manifests are never changed or reused (ADR 0007).
/// </summary>
public sealed class TariffsSerializer(ExtendedActorSystem system)
    : ManifestJsonSerializer(system, Manifests, AddKinds)
{
    /// <summary>The name Akka.NET knows the serializer by, in configuration and logs.</summary>
    public const string Name = "tariffs";

    /// <summary>Every message whose type derives from one of these is written here.</summary>
    public static IReadOnlyList<Type> BoundTypes { get; } =
    [
        typeof(TariffBookCommand),
        typeof(TariffBookEvent),
        typeof(TariffBookQuery),
        typeof(TariffBookReply),
        typeof(TariffBookSnapshot),
    ];

    public static FrozenDictionary<Type, string> Manifests { get; } = new Dictionary<Type, string>
    {
        [typeof(PublishTariff)] = "tariffs.publish-tariff.v1",
        [typeof(RetireTariff)] = "tariffs.retire-tariff.v1",

        [typeof(TariffPublished)] = "tariffs.tariff-published.v1",
        [typeof(TariffRetired)] = "tariffs.tariff-retired.v1",

        [typeof(GetTariffSnapshot)] = "tariffs.get-tariff-snapshot.v1",
        [typeof(CurrentTariff)] = "tariffs.current-tariff.v1",

        [typeof(TariffBookAccepted)] = "tariffs.tariff-book-accepted.v1",
        [typeof(TariffBookRejected)] = "tariffs.tariff-book-rejected.v1",

        [typeof(TariffBookSnapshot)] = "tariffs.tariff-book-snapshot.v1",
    }.ToFrozenDictionary();

    /// <summary>Permanent: Ohmline's serializer ids are listed in ADR 0007.</summary>
    public override int Identifier => 9104;

    /// <summary>
    /// A tariff's components, with the same permanent kinds as in Charging's messages: the
    /// tariff snapshot is one shape, wherever it is written.
    /// </summary>
    private static void AddKinds(JsonSerializerOptions options) =>
        options.AddClosedHierarchy<TariffComponent>(
            (typeof(EnergyRate), "energy-rate"),
            (typeof(TimeRate), "time-rate"),
            (typeof(SessionFee), "session-fee"),
            (typeof(IdleFee), "idle-fee"));
}
