using Ohmline.FakeGridOperator;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SignalBoard>();

var app = builder.Build();
app.MapGet("/health", () => TypedResults.Ok());
app.MapGet("/signals", (long? since, SignalBoard board) =>
    TypedResults.Ok(board.Since(since ?? 0)));
app.MapPost("/signals", (AnnounceEvent announce, SignalBoard board) =>
    TypedResults.Ok(board.Announce(announce)));
app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in tests.</summary>
public partial class Program;
