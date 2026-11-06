using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.ActorShell.Tests.Clustering;

/// <summary>How a node finds its cluster, from its configuration (sections 12.1, 21.6).</summary>
public sealed class ClusterFormationTests
{
    [Fact]
    public void Given_seed_nodes_then_the_node_joins_through_them()
    {
        var formation = FormationOf(
            Environments.Production,
            ("Akka:Cluster:SeedNodes:0", "akka.tcp://ohmline@node-0:4053"));

        formation.ShouldBeOfType<StaticSeedNodes>()
            .Addresses.ShouldBe(["akka.tcp://ohmline@node-0:4053"]);
    }

    [Fact]
    public void Given_a_development_node_without_seed_nodes_then_it_joins_itself()
    {
        FormationOf(Environments.Development).ShouldBeOfType<SelfJoin>();
    }

    [Fact]
    public void Given_a_production_node_without_seed_nodes_then_it_does_not_start()
    {
        Should.Throw<InvalidOperationException>(() => FormationOf(Environments.Production))
            .Message.ShouldContain("OHMLINE_KUBERNETES=true");
    }

    [Fact]
    public void Given_ohmline_kubernetes_then_the_node_bootstraps_from_the_kubernetes_api()
    {
        var formation = FormationOf(
            Environments.Production,
            ("OHMLINE_KUBERNETES", "true"),
            ("Akka:Cluster:SeedNodes:0", "akka.tcp://ohmline@ignored:4053"));

        formation.ShouldBeOfType<KubernetesBootstrap>()
            .ShouldBe(new KubernetesBootstrap
            {
                ServiceName = "ohmline",
                RequiredContactPoints = 2,
                ManagementPort = 8558,
            });
    }

    [Fact]
    public void Given_kubernetes_settings_then_the_bootstrap_takes_them()
    {
        var formation = FormationOf(
            Environments.Production,
            ("OHMLINE_KUBERNETES", "true"),
            ("Ohmline:Kubernetes:RequiredContactPoints", "3"));

        formation.ShouldBeOfType<KubernetesBootstrap>().RequiredContactPoints.ShouldBe(3);
    }

    /// <summary>The formation's case, out of the union, for Shouldly's type assertions.</summary>
    private static object FormationOf(
        string environment, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting =>
                new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

        return ClusterFormation.From(configuration, new Environment(environment)).Value!;
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Ohmline.ActorShell.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
