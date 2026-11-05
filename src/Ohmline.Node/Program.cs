using Akka.Hosting;
using Ohmline.Charging.Actors;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Api;
using Ohmline.Charging.Api.Development;
using Ohmline.Charging.Api.Live;
using Ohmline.DriverAccounts.Actors;
using Ohmline.DriverAccounts.Api;
using Ohmline.Node;
using Ohmline.ServiceDefaults;
using Ohmline.SiteEnergy.Actors;
using Ohmline.SiteEnergy.Api;
using Ohmline.Tariffs.Actors;

var builder = WebApplication.CreateBuilder(args);

// The one clock of the node. The container passes it to every entity it makes for a shard
// region, and the entity reads it once per command into a DecisionContext.
builder.Services.AddSingleton(TimeProvider.System);

builder.AddServiceDefaults();
builder.Services.AddOhmlineReadModels(builder.Configuration);
if (builder.Environment.IsDevelopment())
{
    // The journey's payments and charge points, faked until Chapter 16 brings Billing and
    // the Device Gateway. Outside Development, nothing stands behind the ports yet.
    builder.Services.AddDevelopmentJourneyPorts();
}

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
    .WithOhmlineJournal(builder.Configuration)
    .AddChargingContext()
    .AddSiteEnergyContext()
    .AddDriverAccountsContext()
    .AddTariffsContext()
    .WithOhmlineReadModels(builder.Configuration)
    .AddChargingLiveUpdates());

builder.Services.AddProblemDetails();
builder.Services.AddChargingApi();
builder.Services.AddSiteEnergyApi();
builder.Services.AddDriverAccountsApi();
builder.Services.AddSignalR(); // Live updates for app clients, at /hubs/charging (section 14.5).

var app = builder.Build();

// /alive: the process answers. /health: also an Up member of the cluster, so ready for work.
app.MapDefaultEndpoints();
app.MapChargingEndpoints();
app.MapSiteEnergyEndpoints();
app.MapDriverAccountsEndpoints();
app.MapOhmlineReadModels(app.Configuration);
app.MapChargingHub();
if (app.Environment.IsDevelopment())
{
    app.MapDevScaffoldingEndpoints(); // A pretend charger, until section 16.7.
}

app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests.</summary>
public partial class Program;
