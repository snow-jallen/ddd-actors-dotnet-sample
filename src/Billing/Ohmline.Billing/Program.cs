using Npgsql;
using Ohmline.Billing;
using Ohmline.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddBilling(builder.Configuration);

var app = builder.Build();

// Billing's tables before its first request; Chapter 20 hands this to the migrator.
await BillingSchema.EnsureAsync(
    app.Services.GetRequiredService<NpgsqlDataSource>(), CancellationToken.None);

app.MapDefaultEndpoints();
app.MapBillingEndpoints();

app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in Billing's tests.</summary>
public partial class Program;
