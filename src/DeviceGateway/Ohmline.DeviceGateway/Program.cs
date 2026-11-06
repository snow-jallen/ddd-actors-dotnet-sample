using Akka.Hosting;
using Ohmline.ActorShell.Clustering;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Charging.Actors;
using Ohmline.DeviceGateway;
using Ohmline.DeviceGateway.Connections;
using Ohmline.DeviceGateway.Draining;
using Ohmline.ServiceDefaults;
using Ohmline.Tariffs.Actors;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Cluster.Sharding;
using Petabridge.Cmd.Host;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
// Akka.NET's own logs keep the trace that was current when they were written (section 18.4).
builder.Logging.AddOpenTelemetry(logging => logging.AddAkkaTraceCorrelation());
builder.AddServiceDefaults();
builder.Services.AddOptions<GatewayOptions>()
    .BindConfiguration(GatewayOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
// On the way out, the chargers' connections close in batches, each charger told when to come
// back, all within the 30 s the host gives its services to stop (section 21.7).
builder.Services.AddConnectionDrain();
builder.Services.Configure<HostOptions>(host =>
    host.ShutdownTimeout = ShutdownBudget.HostShutdownTimeout);

// A member of Ohmline's cluster with the role gateway: it hosts no aggregate, and reaches
// Charging's through proxies to its regions, and the tariff book through its singleton proxy.
builder.Services.AddAkka("ohmline", akka => akka
    .ConfigureLoggers(loggers =>
    {
        loggers.ClearLoggers();
        loggers.AddLoggerFactory();
    })
    .WithOhmlineCluster(
        builder.Configuration,
        builder.Environment,
        ClusterRoles.Gateway,
        OhmlineAppVersion.Of(typeof(Program).Assembly))
    .WithShutdownBudget()
    .WithConnectionDrain()
    .WithShellMetrics() // Dead letters and actors' lifecycles; it hosts no shard (section 18.2).
    // Operators' commands (pbm): cluster and sharding, on the loopback interface only, never
    // Petabridge.Cmd's default 0.0.0.0. Reached with port forwarding (section 18.8).
    .AddPetabridgeCmd(
        new PetabridgeCmdOptions
        {
            Host = "127.0.0.1",
            Port = builder.Configuration.GetValue("PetabridgeCmd:Port", 9110),
        },
        commands =>
        {
            commands.RegisterCommandPalette(ClusterCommands.Instance);
            commands.RegisterCommandPalette(ClusterShardingCommands.Instance);
        })
    .AddChargingProxies()
    .AddTariffsProxy());

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.MapDefaultEndpoints();
app.MapChargerSockets();

app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in tests.</summary>
public partial class Program;
