using Akka.Hosting;
using Ohmline.ActorShell.Clustering;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Charging.Actors;
using Ohmline.DeviceGateway;
using Ohmline.DeviceGateway.Connections;
using Ohmline.ServiceDefaults;
using Ohmline.Tariffs.Actors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
// Akka.NET's own logs keep the trace that was current when they were written (section 18.4).
builder.Logging.AddOpenTelemetry(logging => logging.AddAkkaTraceCorrelation());
builder.AddServiceDefaults();
builder.Services.AddOptions<GatewayOptions>()
    .BindConfiguration(GatewayOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// A member of Ohmline's cluster with the role gateway: it hosts no aggregate, and reaches
// Charging's through proxies to its regions, and the tariff book through its singleton proxy.
builder.Services.AddAkka("ohmline", akka => akka
    .ConfigureLoggers(loggers =>
    {
        loggers.ClearLoggers();
        loggers.AddLoggerFactory();
    })
    .WithOhmlineCluster(builder.Configuration, builder.Environment, ClusterRoles.Gateway)
    .WithShellMetrics() // Dead letters and actors' lifecycles; it hosts no shard (section 18.2).
    .AddChargingProxies()
    .AddTariffsProxy());

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.MapDefaultEndpoints();
app.MapChargerSockets();

app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in tests.</summary>
public partial class Program;
