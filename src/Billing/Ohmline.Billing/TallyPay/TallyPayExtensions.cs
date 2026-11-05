using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Ohmline.Billing.TallyPay;

public static class TallyPayExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The TallyPay client, with a resilience handler of its own in place of the host's
        /// default: attempts of 3 s, three retries from 250 ms, 15 s in all. Retrying a POST is
        /// safe here and only here, because every TallyPay call carries an idempotency key.
        /// </summary>
        public IServiceCollection AddTallyPay()
        {
            services.AddOptions<TallyPayOptions>()
                .BindConfiguration(TallyPayOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            // The host's default handler (AddServiceDefaults) would wrap this one: two retry
            // loops, up to 16 calls for one. RemoveAllResilienceHandlers is still experimental
            // in Microsoft.Extensions.Http.Resilience 10.11 (EXTEXP0001); section 16.4.
#pragma warning disable EXTEXP0001
            services
                .AddHttpClient<TallyPayClient>((provider, http) =>
                    http.BaseAddress = provider
                        .GetRequiredService<IOptions<TallyPayOptions>>().Value.BaseAddress)
                .RemoveAllResilienceHandlers()
                .AddStandardResilienceHandler(Configure);
#pragma warning restore EXTEXP0001
            return services;
        }
    }

    private static void Configure(HttpStandardResilienceOptions resilience)
    {
        resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        resilience.Retry.MaxRetryAttempts = 3;
        resilience.Retry.Delay = TimeSpan.FromMilliseconds(250);
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    }
}
