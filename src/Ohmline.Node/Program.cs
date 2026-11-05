using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using Ohmline.Charging.Actors;
using Ohmline.SiteEnergy.Actors;

var builder = WebApplication.CreateBuilder(args);

// The one clock of the node. Actors read it once per command into a DecisionContext.
builder.Services.AddSingleton(TimeProvider.System);

var journal = builder.Configuration.GetConnectionString("journal")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:journal is not set. In Development, appsettings.Development.json " +
        "sets it to a SQLite file.");

builder.Services.AddAkka("ohmline", akka => akka
    .ConfigureLoggers(loggers =>
    {
        loggers.ClearLoggers();
        loggers.AddLoggerFactory(); // Akka.NET logs through Microsoft.Extensions.Logging.
    })
    .WithSqlPersistence(
        connectionString: journal,
        providerName: ProviderName.SQLiteMS,
        autoInitialize: true, // Creates missing tables; Chapter 20 owns the schema instead.
        tagStorageMode: TagMode.TagTable)
    .AddChargingContext()
    .AddSiteEnergyContext());

var app = builder.Build();

// Liveness only: the process is up and serving HTTP. Readiness arrives with the cluster.
app.MapGet("/alive", () => TypedResults.Text("alive"));

app.Run();
