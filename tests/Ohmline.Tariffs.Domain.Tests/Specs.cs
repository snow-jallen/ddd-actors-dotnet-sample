// The Given-When-Then harness, closed over the tariff book's decider once for every test.
global using TariffBookSpec = Ohmline.Testing.DeciderSpec<
    Ohmline.Tariffs.Domain.Book.TariffBook,
    Ohmline.Tariffs.Domain.Book.TariffBookState,
    Ohmline.Tariffs.Contracts.Book.TariffBookCommand,
    Ohmline.Tariffs.Domain.Book.TariffBookEvent,
    Ohmline.Tariffs.Domain.Book.TariffBookError>;
