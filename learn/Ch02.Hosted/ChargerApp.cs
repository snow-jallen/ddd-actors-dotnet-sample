using System.Text.Json.Serialization;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Ch02.AkkaBasics.Tests.Chargers;

namespace Ch02.Hosted;

public static class ChargerApp
{
    /// <summary>This host runs a single twin. Chapter 12 replaces it with a shard region.</summary>
    public const string ChargePointId = "OHM-SLC-0042";

    public static WebApplication Create(
        string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddAkka("learn", akka => akka
            .ConfigureLoggers(loggers =>
            {
                loggers.ClearLoggers();
                loggers.AddLoggerFactory(); // Akka.NET logs through Microsoft.Extensions.Logging.
            })
            .WithActorAskTimeout(TimeSpan.FromSeconds(5)) // For any Ask that passes no timeout.
            .WithActors((system, registry) =>
            {
                var twin = system.ActorOf(ChargerTwin.Props(ChargePointId), "charger-twin");
                registry.Register<ChargerTwin>(twin);
            })
            .AddStartup((system, _) =>
                CoordinatedShutdown.Get(system).AddTask(
                    CoordinatedShutdown.PhaseBeforeServiceUnbind,
                    "drain-charger-twins",
                    () =>
                    {
                        system.Log.Info("Host is stopping: draining charger twins");
                        return Task.FromResult(Done.Instance);
                    })));

        configure?.Invoke(builder);

        var app = builder.Build();
        app.MapChargerEndpoints();
        return app;
    }
}
