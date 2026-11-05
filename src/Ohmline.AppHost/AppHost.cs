using System.Globalization;

// Ohmline on a laptop: PostgreSQL 17 and a cluster of three nodes (section 12.10).
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithImageTag("17");
var ohmline = postgres.AddDatabase("ohmline");

// The first node is the seed: it listens for Akka.Remote on a port fixed here, and every node,
// itself included, names that address as its seed. The seed joins itself and forms the
// cluster; the others join through it, once it is healthy (an Up member, and the journal's
// tables made). A node is a resource of its own because a replica made with WithReplicas gets
// its remoting port only at run time, and a static seed must be known in advance (ADR 0010).
var seed = Node("node", 0).WaitFor(ohmline);
Node("node-2", 1).WaitFor(seed);
Node("node-3", 2).WaitFor(seed);

builder.Build().Run();

IResourceBuilder<ProjectResource> Node(string name, int index) =>
    builder.AddProject<Projects.Ohmline_Node>(name, launchProfileName: null)
        .WithHttpEndpoint(port: 5180 + index, name: "http")
        .WithHttpHealthCheck("/health")
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("Akka__Remote__Port", Port(4053 + index))
        .WithEnvironment("Akka__Cluster__SeedNodes__0", "akka.tcp://ohmline@localhost:4053")
        .WithReference(ohmline);

static string Port(int port) => port.ToString(CultureInfo.InvariantCulture);
