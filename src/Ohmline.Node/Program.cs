using Akka.Hosting;
using Akka.Persistence.Hosting;
using Ohmline.Charging.Actors;

var builder = WebApplication.CreateBuilder(args);

// The one clock of the node. Actors read it once per command into a DecisionContext.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddAkka("ohmline", akka => akka
    .ConfigureLoggers(loggers =>
    {
        loggers.ClearLoggers();
        loggers.AddLoggerFactory(); // Akka.NET logs through Microsoft.Extensions.Logging.
    })
    .WithInMemoryJournal() // For now: forgets everything on restart (section 10.7 fixes that).
    .WithInMemorySnapshotStore()
    .AddChargingContext());

var app = builder.Build();

// Liveness only: the process is up and serving HTTP. Readiness arrives with the cluster.
app.MapGet("/alive", () => TypedResults.Text("alive"));

app.Run();
