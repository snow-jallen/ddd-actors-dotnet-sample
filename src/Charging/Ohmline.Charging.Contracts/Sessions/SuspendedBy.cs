namespace Ohmline.Charging.Contracts.Sessions;

/// <summary>
/// Who suspended charging: the car, which stopped drawing power (it is full, or its own schedule
/// says so), or the charge point, because Ohmline lowered its allocation to nothing during a
/// curtailment. Charge points report which (OCPP's <c>SuspendedEV</c> and <c>SuspendedEVSE</c>),
/// and the idle fee depends on it (section 8.6).
/// </summary>
public closed record SuspendedBy;

/// <summary>The car stopped charging; the driver may be asked to move it.</summary>
public sealed record ByEv : SuspendedBy;

/// <summary>The charge point stopped charging; the driver is waiting for Ohmline.</summary>
public sealed record ByCharger : SuspendedBy;
