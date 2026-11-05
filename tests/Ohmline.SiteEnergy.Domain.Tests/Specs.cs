// The Given-When-Then harness, closed over the site power budget once for every test.
global using SitePowerBudgetSpec = Ohmline.Testing.DeciderSpec<
    Ohmline.SiteEnergy.Domain.Budgets.SitePowerBudget,
    Ohmline.SiteEnergy.Domain.Budgets.SitePowerBudgetState,
    Ohmline.SiteEnergy.Contracts.Budgets.SitePowerBudgetCommand,
    Ohmline.SiteEnergy.Domain.Budgets.SitePowerBudgetEvent,
    Ohmline.SiteEnergy.Domain.Budgets.SitePowerBudgetError>;
