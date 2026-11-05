using Ohmline.Charging.Actors.Journeys.Ports;

namespace Ohmline.Node;

/// <summary>What stands behind the charging journey's ports in a node.</summary>
public static class JourneyPortExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Payments through Billing's hold API (section 16.5), at <c>http://billing</c>, which
        /// service discovery resolves: the Aspire resource, or <c>Services:billing</c> in
        /// configuration. The host's resilience handler may send a request again; Billing's
        /// API is idempotent by hold id. Charge points through the Device Gateway (section
        /// 16.7), on each charge point's DistributedPubSub topic.
        /// </summary>
        public IServiceCollection AddJourneyPorts()
        {
            services.AddHttpClient<IPaymentPort, BillingPaymentPort>(billing =>
                billing.BaseAddress = new Uri("http://billing"));
            return services.AddSingleton<IChargerCommandPort, GatewayChargerCommandPort>();
        }
    }
}
