using Akka.Configuration;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.ActorShell.Tests.Clustering;

/// <summary>
/// The Akka.NET configuration of a node with <c>OHMLINE_KUBERNETES=true</c> (section 21.6), read
/// from the builder without starting the node: outside Kubernetes, discovery and the lease find
/// no API server, and cluster bootstrap stops the actor system on purpose.
/// </summary>
public sealed class KubernetesClusterTests
{
    private static readonly Config Kubernetes = ConfigOf(new Dictionary<string, string?>
    {
        ["OHMLINE_KUBERNETES"] = "true",
        ["Akka:Remote:Hostname"] = "0.0.0.0",
        ["Akka:Remote:PublicHostname"] = "10.244.1.17",
        ["Akka:Remote:Port"] = "4053",
    });

    [Fact]
    public void Given_kubernetes_then_contact_points_are_the_labelled_pods_from_the_api()
    {
        Kubernetes.GetString("akka.discovery.method").ShouldBe("kubernetes-api");
        Kubernetes.GetString("akka.discovery.kubernetes-api.pod-label-selector")
            .ShouldBe("akka-cluster={0}");
        Kubernetes.GetString(Bootstrap("service-name")).ShouldBe("ohmline");
        Kubernetes.GetString(Bootstrap("port-name")).ShouldBe("management");
        Kubernetes.GetInt(Bootstrap("required-contact-point-nr")).ShouldBe(2);
        Kubernetes.GetStringList("akka.cluster.seed-nodes").ShouldBeEmpty();
    }

    [Fact]
    public void Given_kubernetes_then_remoting_binds_every_interface_and_advertises_the_pod()
    {
        Kubernetes.GetString("akka.remote.dot-netty.tcp.hostname").ShouldBe("0.0.0.0");
        Kubernetes.GetString("akka.remote.dot-netty.tcp.public-hostname").ShouldBe("10.244.1.17");
        Kubernetes.GetString("akka.management.http.hostname").ShouldBe("10.244.1.17");
        Kubernetes.GetString("akka.management.http.bind-hostname").ShouldBe("0.0.0.0");
        Kubernetes.GetInt("akka.management.http.port").ShouldBe(8558);
    }

    [Fact]
    public void Given_kubernetes_then_the_side_that_holds_the_lease_survives_a_partition()
    {
        Kubernetes.GetString("akka.cluster.split-brain-resolver.active-strategy")
            .ShouldBe("lease-majority");
        Kubernetes.GetString(
                "akka.cluster.split-brain-resolver.lease-majority.lease-implementation")
            .ShouldBe("akka.coordination.lease.kubernetes");
        Kubernetes.GetString("akka.coordination.lease.kubernetes.lease-class")
            .ShouldStartWith("Akka.Coordination.KubernetesApi.KubernetesLease");
    }

    [Fact]
    public void Given_seed_nodes_then_the_majority_keeps_the_cluster_without_a_lease()
    {
        var seeded = ConfigOf(new Dictionary<string, string?>
        {
            ["Akka:Cluster:SeedNodes:0"] = "akka.tcp://ohmline@node-0:4053",
        });

        seeded.GetString("akka.cluster.split-brain-resolver.active-strategy")
            .ShouldBe("keep-majority");
        seeded.HasPath("akka.management.http.port").ShouldBeFalse();
    }

    private static string Bootstrap(string key) =>
        $"akka.management.cluster.bootstrap.contact-point-discovery.{key}";

    private static Config ConfigOf(Dictionary<string, string?> settings)
    {
        var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "ohmline");
        builder.WithOhmlineCluster(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            new Production(),
            "charging",
            "1.0.0");
        return builder.Configuration.Value;
    }

    private sealed class Production : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Ohmline.ActorShell.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
