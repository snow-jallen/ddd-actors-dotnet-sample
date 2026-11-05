// The Given-When-Then harness, closed over the id token's decider once for every test.
global using IdTokenSpec = Ohmline.Testing.DeciderSpec<
    Ohmline.DriverAccounts.Domain.Tokens.IdToken,
    Ohmline.DriverAccounts.Domain.Tokens.IdTokenState,
    Ohmline.DriverAccounts.Contracts.Tokens.IdTokenCommand,
    Ohmline.DriverAccounts.Domain.Tokens.IdTokenEvent,
    Ohmline.DriverAccounts.Domain.Tokens.IdTokenError>;
