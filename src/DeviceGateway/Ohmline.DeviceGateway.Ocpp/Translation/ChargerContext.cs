using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.DeviceGateway.Ocpp.Translation;

/// <summary>
/// What the gateway remembers about one connected charger, so that a frame can be translated:
/// each connector's last status, the starts Ohmline has asked for and not yet seen, and the
/// OCPP transactions running, each with the Ohmline session it is. Immutable: the translator
/// returns the next context with what it translated.
/// </summary>
public sealed record ChargerContext(
    ChargePointId ChargePointId,
    StartingTerms? Terms,
    ImmutableDictionary<int, string> Statuses,
    ImmutableDictionary<int, PendingStart> PendingStarts,
    ImmutableDictionary<int, OpenTransaction> Transactions)
{
    public static ChargerContext For(ChargePointId chargePointId) => new(
        chargePointId,
        Terms: null,
        ImmutableDictionary<int, string>.Empty,
        ImmutableDictionary<int, PendingStart>.Empty,
        ImmutableDictionary<int, OpenTransaction>.Empty);

    /// <summary>Ohmline asked the charger to start this session on the connector.</summary>
    public ChargerContext Expecting(int connector, SessionId sessionId, TokenUid tokenUid) =>
        this with
        {
            PendingStarts = PendingStarts.SetItem(connector, new PendingStart(sessionId, tokenUid)),
        };

    public OpenTransaction? OnConnector(int connector) =>
        Transactions.Values.FirstOrDefault(transaction => transaction.ConnectorId == connector);

    public OpenTransaction? Transaction(int transactionId) =>
        Transactions.GetValueOrDefault(transactionId);
}

/// <summary>
/// What a session started at this charger is started under: the charge point's site, the
/// tariff current when the gateway last asked, and the spend cap the gateway is configured with.
/// </summary>
public sealed record StartingTerms(SiteId SiteId, TariffSnapshot Tariff, Money SpendCap);

/// <summary>A start Ohmline asked for: the session it will be, for this token.</summary>
public sealed record PendingStart(SessionId SessionId, TokenUid TokenUid);

/// <summary>An OCPP transaction on a connector, and the Ohmline session it is.</summary>
public sealed record OpenTransaction(
    int TransactionId, int ConnectorId, SessionId SessionId, TokenUid TokenUid);
