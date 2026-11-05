using Ohmline.Billing;
using Ohmline.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddBilling(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapBillingEndpoints();

app.Run();

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in Billing's tests.</summary>
public partial class Program;
