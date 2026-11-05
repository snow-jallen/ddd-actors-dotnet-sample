namespace Ohmline.Charging.Api.Queries;

/// <summary>A site's connectors as the operator's live board shows them.</summary>
public sealed record SiteLiveBoardResponse(
    string SiteId, IReadOnlyList<ConnectorOnBoardResponse> Connectors);

/// <summary>
/// One connector on the board. <paramref name="Status"/> is a connector status, or
/// <c>unknown</c> until the charge point's registration has been projected;
/// <paramref name="AllocatedKw"/> is null when no session has asked for power, 0 while one
/// waits.
/// </summary>
public sealed record ConnectorOnBoardResponse(
    string ConnectorId, string Status, Guid? SessionId, decimal? AllocatedKw);
