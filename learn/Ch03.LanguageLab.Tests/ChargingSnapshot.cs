namespace Ch03.LanguageLab.Tests;

/// <summary>
/// What a connector reports at one moment: whether a car is plugged in, the power it draws, and
/// how long it has drawn none.
/// </summary>
public sealed record ChargingSnapshot(bool PluggedIn, Kilowatts Power, TimeSpan IdleFor);
