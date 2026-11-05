using Ohmline.ActorShell.Serialization;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Serialization.Upcasting;

/// <summary>
/// <c>charging.meter-value-recorded.v1</c>, the shape meter readings were stored in until
/// section 20.3: the register as the charger's integer count of watt-hours. Kept for ever, here
/// and nowhere else, so the serializer can read the readings already in the journal; the domain
/// only ever sees today's <see cref="MeterValueRecorded"/>.
/// </summary>
internal sealed record MeterValueRecordedV1(
    SessionId SessionId, long RegisterWh, DateTimeOffset MeasuredAt)
{
    public CommandId CommandId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Wh to kWh, exactly; a v1 reading was always the total over every phase.</summary>
    public static Upcaster Upcaster { get; } =
        Upcaster.Create<MeterValueRecordedV1, MeterValueRecorded>(v1 =>
            new MeterValueRecorded(
                v1.SessionId, Energy.FromWattHours(v1.RegisterWh), v1.MeasuredAt, Phase: null)
            {
                CommandId = v1.CommandId,
                OccurredAt = v1.OccurredAt,
            });
}
