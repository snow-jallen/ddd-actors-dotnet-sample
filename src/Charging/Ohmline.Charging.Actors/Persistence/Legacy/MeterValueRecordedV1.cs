using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Persistence.Legacy;

/// <summary>
/// <c>charging.meter-value-recorded.v1</c>, the shape meter readings were stored in until
/// section 20.3: the register as the charger's integer count of watt-hours. On this branch the
/// serializer reads it as it is, and <see cref="LegacyEventsReadAdapter"/> upcasts it, so this
/// type must stay readable, and registered, for as long as the journal holds one.
/// </summary>
public sealed record MeterValueRecordedV1(
    SessionId SessionId, long RegisterWh, DateTimeOffset MeasuredAt)
{
    public CommandId CommandId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Wh to kWh, exactly; a v1 reading was always the total over every phase.</summary>
    public MeterValueRecorded ToV2() =>
        new(SessionId, Energy.FromWattHours(RegisterWh), MeasuredAt, Phase: null)
        {
            CommandId = CommandId,
            OccurredAt = OccurredAt,
        };
}
