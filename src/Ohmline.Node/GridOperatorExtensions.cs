using Akka.Hosting;
using Ohmline.SiteEnergy.Acl.GridOperator;
using Ohmline.SiteEnergy.Acl.GridOperator.Northgate;

namespace Ohmline.Node;

/// <summary>
/// Northgate Grid's demand-response signals, when <c>Ohmline:SiteEnergy:GridOperator:Enabled</c>
/// is set (section 16.8): the feed at <c>http://fakegrid</c>, which service discovery resolves,
/// and the poller as a cluster singleton. Off, the sites are never curtailed.
/// </summary>
public static class GridOperatorExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddGridOperator(IConfiguration configuration)
        {
            services.AddOptions<GridOperatorOptions>()
                .BindConfiguration(GridOperatorOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            if (IsEnabled(configuration))
            {
                services.AddHttpClient<INorthgateFeed, NorthgateClient>(northgate =>
                    northgate.BaseAddress = new Uri("http://fakegrid"));
            }

            return services;
        }
    }

    extension(AkkaConfigurationBuilder akka)
    {
        public AkkaConfigurationBuilder WithGridOperator(IConfiguration configuration) =>
            IsEnabled(configuration) ? akka.AddGridOperatorAcl() : akka;
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>($"{GridOperatorOptions.Section}:Enabled");
}
