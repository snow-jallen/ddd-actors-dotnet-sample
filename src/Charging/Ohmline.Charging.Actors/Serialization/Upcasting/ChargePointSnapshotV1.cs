using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ohmline.ActorShell.Deciders;
using Ohmline.ActorShell.Serialization;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Serialization.Upcasting;

/// <summary>
/// <c>charging.charge-point-snapshot.v1</c>, the shape charge point snapshots were stored in
/// until section 20.6: each connector a case written with its <c>"$kind"</c>, a reservation
/// with the time it expires. Read here as a plain document and upcast to today's snapshot.
/// </summary>
internal sealed record ChargePointSnapshotV1(
    ChargePointId ChargePointId,
    SiteId SiteId,
    ImmutableArray<ChargePointSnapshotV1.Connector> Connectors)
{
    public ImmutableArray<RecentCommand> RecentCommands { get; init; } = [];

    /// <summary>
    /// A v1 snapshot never said when it was taken. Any time gives every reservation its exact
    /// expiry back; the earliest expiry keeps every offset at zero or more.
    /// </summary>
    public static Upcaster Upcaster { get; } =
        Upcaster.Create<ChargePointSnapshotV1, ChargePointSnapshot>(v1 =>
        {
            var takenAt = v1.Connectors
                .Select(connector => connector.ExpiresAt)
                .OfType<DateTimeOffset>()
                .DefaultIfEmpty(DateTimeOffset.UnixEpoch)
                .Min();
            return new ChargePointSnapshot(
                v1.ChargePointId,
                v1.SiteId,
                takenAt,
                [.. v1.Connectors.Select(connector => connector.Upcast(takenAt))])
            {
                RecentCommands = v1.RecentCommands.IsDefault ? [] : v1.RecentCommands,
            };
        });

    /// <summary>Every field any v1 connector case had; the kind says which ones it has.</summary>
    internal sealed record Connector(
        [property: JsonPropertyName("$kind")] string Kind,
        int Number,
        Power MaxPower,
        TokenUid? TokenUid = null,
        DateTimeOffset? ExpiresAt = null,
        SessionId? SessionId = null,
        string? ErrorCode = null)
    {
        public ConnectorSnapshot Upcast(DateTimeOffset takenAt) => Kind switch
        {
            "available" => new ConnectorSnapshot.Available(Number, MaxPower),
            "reserved" => new ConnectorSnapshot.Reserved(
                Number, MaxPower, Required(TokenUid), Required(ExpiresAt) - takenAt),
            "occupied" => new ConnectorSnapshot.Occupied(Number, MaxPower, Required(SessionId)),
            "faulted" => new ConnectorSnapshot.Faulted(Number, MaxPower, Required(ErrorCode)),
            "unavailable" => new ConnectorSnapshot.Unavailable(Number, MaxPower),
            _ => throw new System.Text.Json.JsonException(
                $"A v1 charge point snapshot has no connector kind '{Kind}'."),
        };

        private T Required<T>(T? value)
            where T : struct =>
            value ?? throw Missing<T>();

        private T Required<T>(T? value)
            where T : class =>
            value ?? throw Missing<T>();

        private System.Text.Json.JsonException Missing<T>() =>
            new($"A v1 '{Kind}' connector snapshot has no {typeof(T).Name}.");
    }
}
