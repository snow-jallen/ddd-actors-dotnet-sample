namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// The driver's id token in the app, such as <c>04A2B3C4D5E6F7</c>: the credential that starts
/// a charge and whose cards the app shows. Where it comes from is the head's business.
/// </summary>
public sealed record DriverToken(string Uid);
