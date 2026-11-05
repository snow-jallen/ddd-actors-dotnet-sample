using Ohmline.FakePsp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TallyPayLedger>();
builder.Services.AddSingleton<Flakiness>();

var app = builder.Build();
app.MapGet("/health", () => TypedResults.Ok());
app.MapTallyPay();
app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in its tests.</summary>
public partial class Program;
