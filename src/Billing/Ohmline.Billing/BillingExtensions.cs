using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Ohmline.Billing.Feed;
using Ohmline.Billing.Holds;
using Ohmline.Billing.Invoicing;
using Ohmline.Billing.Outbox;
using Ohmline.Billing.Receipts;

namespace Ohmline.Billing;

/// <summary>Billing's services and endpoints, in one call each.</summary>
public static class BillingExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The database in <c>ConnectionStrings:billing</c>, the hold store, the feed consumer
        /// and the outbox dispatcher, with the feed's settings checked at start.
        /// </summary>
        public IServiceCollection AddBilling(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("billing")
                ?? throw new InvalidOperationException("ConnectionStrings:billing is not set.");
            services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<HoldStore>();
            services.AddSingleton<InvoiceIssuer>();
            services.AddSingleton<IReceiptSender, LoggingReceiptSender>();
            services.AddOptions<ChargingFeedOptions>()
                .BindConfiguration(ChargingFeedOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddHttpClient<ChargingFeedClient>((provider, http) =>
                http.BaseAddress = provider
                    .GetRequiredService<IOptions<ChargingFeedOptions>>().Value.BaseAddress);
            services.AddHostedService<ChargingFeedConsumer>();
            services.AddHostedService<OutboxDispatcher>();
            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapBillingEndpoints() =>
            endpoints.MapHoldEndpoints().MapReceiptsEndpoints();
    }
}
