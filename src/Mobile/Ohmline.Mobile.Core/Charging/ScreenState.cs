namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// What a screen is doing, in the app's words: waiting for an answer, ready for the driver,
/// offline, or stopped by a failure it can explain. A union, so a page that renders it in a
/// switch expression does not compile until it renders every case (section 19.5).
/// </summary>
public union ScreenState(Loading, Ready, Offline, Failed);

/// <summary>Waiting for the server.</summary>
public sealed record Loading;

/// <summary>Ready for the driver.</summary>
public sealed record Ready;

/// <summary>The phone has no usable connection, as far as it can tell.</summary>
public sealed record Offline;

/// <summary>Something went wrong that the driver should hear about, in a sentence.</summary>
public sealed record Failed(string Message);
