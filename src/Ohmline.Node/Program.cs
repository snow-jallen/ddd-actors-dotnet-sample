using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using Ohmline.Charging.Actors;
using Ohmline.Node;
using Ohmline.SiteEnergy.Actors;

var builder = WebApplication.CreateBuilder(args);

// The one clock of the node. The container passes it to every entity it makes for a shard
// region, and the entity reads it once per command into a DecisionContext.
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
    .WithOhmlineCluster(builder.Configuration, builder.Environment)
    // At most this many persistent actors replay their journals at once; the rest wait for a
    // permit, so a node restart does not stampede the database (section 11.3). 50 is Akka.NET's
    // default, written down here so that it is a decision and not an accident.
    .AddHocon("akka.persistence.max-concurrent-recoveries = 50", HoconAddMode.Prepend)
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
