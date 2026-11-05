namespace Ohmline.DriverAccounts.Api.Requests;

/// <summary>A card's UID, such as <c>04A2B3C4D5E6F7</c>, and the account it is issued to.</summary>
public sealed record RegisterTokenRequest(string TokenUid, string Driver);
