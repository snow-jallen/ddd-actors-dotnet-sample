namespace Ohmline.Tariffs.Contracts.Book;

/// <summary>
/// The key under which a node's actor registry holds the proxy to the tariff book, the cluster
/// singleton <c>tariff-book</c>: code that asks the book for a tariff asks for
/// <c>IRequiredActor&lt;TariffBookProxy&gt;</c>, wherever in the cluster the book runs.
/// </summary>
public sealed class TariffBookProxy;
