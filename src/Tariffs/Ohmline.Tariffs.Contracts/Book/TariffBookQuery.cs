namespace Ohmline.Tariffs.Contracts.Book;

/// <summary>A question for the tariff book: a family of its own, never a command.</summary>
public closed record TariffBookQuery;

/// <summary>Which version of this tariff applies to a session that starts now?</summary>
public sealed record GetTariffSnapshot(TariffId TariffId) : TariffBookQuery;
