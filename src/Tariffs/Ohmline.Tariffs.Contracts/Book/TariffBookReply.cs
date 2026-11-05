namespace Ohmline.Tariffs.Contracts.Book;

/// <summary>
/// What the tariff book answers. A closed record, not a union, because it is told back across
/// the cluster (ADR 0003).
/// </summary>
public closed record TariffBookReply;

/// <summary>Every event the command caused is stored; the version is the book's now.</summary>
public sealed record TariffBookAccepted(long Version) : TariffBookReply;

/// <summary>Refused and nothing recorded: a stable code, and a sentence for people.</summary>
public sealed record TariffBookRejected(string Code, string Message) : TariffBookReply;

/// <summary>
/// The answer to <see cref="GetTariffSnapshot"/>: the version a session starting now copies, or
/// none when the tariff was never published or is retired.
/// </summary>
public sealed record CurrentTariff(TariffId TariffId, TariffSnapshot? Snapshot) : TariffBookReply;
