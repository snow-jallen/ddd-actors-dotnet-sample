using Akka.Hosting;
using Ohmline.ActorShell.Clustering;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Charging.Actors;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Api;
using Ohmline.Charging.Api.Live;
using Ohmline.DriverAccounts.Actors;
using Ohmline.DriverAccounts.Api;
using Ohmline.Node;
using Ohmline.ServiceDefaults;
using Ohmline.SiteEnergy.Actors;
using Ohmline.SiteEnergy.Actors.Diagnostics;
using Ohmline.SiteEnergy.Api;
using Ohmline.Tariffs.Actors;

var builder = WebApplication.CreateBuilder(args);

// The one clock of the node. The container passes it to every entity it makes for a shard
// region, and the entity reads it once per command into a DecisionContext.
builder.Services.AddSingleton(TimeProvider.System);

// Each context's instruments, on a meter the host's IMeterFactory makes (section 18.1). The
// container passes them to the entities too, as it passes the clock.
builder.Services.AddSingleton<ChargingMetrics>();
builder.Services.AddSingleton<SiteEnergyMetrics>();

// Akka.NET logs from a logger actor, later and on another thread, so each log keeps the trace
// that was current when it was written, and this stamps it on the record (section 18.4). Ahead
// of the service defaults, so it runs before every exporter they add.
builder.Logging.AddOpenTelemetry(logging => logging.AddAkkaTraceCorrelation());
builder.AddServiceDefaults();
builder.Services.AddOhmlineReadModels(builder.Configuration);
builder.Services.AddJourneyPorts(); // Billing, and the Device Gateway's charge points.
builder.Services.AddGridOperator(builder.Configuration); // Northgate's signals, if enabled.

builder.Services.AddAkka("ohmline", akka => akka
    .ConfigureLoggers(loggers =>
    {
        loggers.ClearLoggers();
        // Akka.NET logs through Microsoft.Extensions.Logging, every one under the category
        // Akka.Actor.ActorSystem, whatever actor wrote it (section 18.4).
        loggers.AddLoggerFactory();
    })
    .WithOhmlineCluster(builder.Configuration, builder.Environment, ClusterRoles.Charging)
    // Dead letters, actors' lifecycles and the shard regions' size (section 18.2).
    .WithShellMetrics()
    .WithShardStats()
    // At most this many persistent actors replay their journals at once; the rest wait for a
    // permit, so a node restart does not stampede the database (section 11.3). 50 is Akka.NET's
    // default, written down here so that it is a decision and not an accident.
    .AddHocon("akka.persistence.max-concurrent-recoveries = 50", HoconAddMode.Prepend)
    .WithOhmlineJournal(builder.Configuration)
    .AddChargingContext()
    .AddSiteEnergyContext()
    .AddDriverAccountsContext()
    .AddTariffsContext()
    .WithGridOperator(builder.Configuration)
    .WithOhmlineReadModels(builder.Configuration)
    .AddChargingLiveUpdates());

builder.Services.AddProblemDetails();
builder.Services.AddChargingApi();
builder.Services.AddSiteEnergyApi();
builder.Services.AddDriverAccountsApi();
builder.Services.AddSignalR(); // Live updates for app clients, at /hubs/charging (section 14.5).

var app = builder.Build();

// /health/live: the actor system runs. /health/ready: also an Up member of the cluster that
// reaches its journal's database, so ready for work (section 18.5).
app.MapDefaultEndpoints();
app.MapChargingEndpoints();
app.MapSiteEnergyEndpoints();
app.MapDriverAccountsEndpoints();
app.MapOhmlineReadModels(app.Configuration);
app.MapChargingHub();

app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests.</summary>
public partial class Program;
