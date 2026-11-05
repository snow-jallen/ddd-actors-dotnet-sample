using System.Globalization;

// Ohmline on a laptop: PostgreSQL 17 and a cluster of three nodes (section 12.10), Billing and
// the TallyPay stand-in (section 16.5), the Device Gateway and a charger simulator (16.7), and
// the Northgate Grid stand-in (16.8).
var builder = DistributedApplication.CreateBuilder(args);

// The seed node's Akka.Remote address, which every member of the cluster joins.
const string SeedNode = "akka.tcp://ohmline@localhost:4053";

var postgres = builder.AddPostgres("postgres").WithImageTag("17");
var ohmline = postgres.AddDatabase("ohmline");
var billingDb = postgres.AddDatabase("billingdb", databaseName: "billing");

// TallyPay, as the stand-in plays it: one request in ten fails, so the resilience handlers
// and the idempotency keys have something to do (section 16.4).
var fakePsp = builder.AddProject<Projects.Ohmline_FakePsp>("fakepsp", launchProfileName: null)
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("FLAKY_RATE", "0.1");

// Northgate Grid, as the stand-in plays it: announce an event with POST /signals, and the
// node's poller curtails the site its VEN id is mapped to.
var fakeGrid = builder.AddProject<Projects.Ohmline_FakeGridOperator>(
        "fakegrid", launchProfileName: null)
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health");

// Billing: its own database, TallyPay behind it, and Charging's feed, which it reads from the
// seed node (http://node). Charging's nodes call its hold API at http://billing. Its endpoint
// is the one in its launch profile (http://localhost:5185), named "http" after its scheme.
var billing = builder.AddProject<Projects.Ohmline_Billing>("billing")
    .WithHttpHealthCheck("/health/ready")
    .WithReference(billingDb, connectionName: "billing")
    .WithReference(fakePsp)
    .WaitFor(billingDb);

// The nodes do not use their launch profile: it would give all three the same address. Each
// declares an endpoint of its own instead (section 16.9).
//
// The first node is the seed: it listens for Akka.Remote on a port fixed here, and every node,
// itself included, names that address as its seed. The seed joins itself and forms the
// cluster; the others join through it, once it is healthy (an Up member, and the journal's
// tables made). A node is a resource of its own because a replica made with WithReplicas gets
// its remoting port only at run time, and a static seed must be known in advance (ADR 0010).
var seed = Node("node", 0).WaitFor(ohmline);
Node("node-2", 1).WaitFor(seed);
Node("node-3", 2).WaitFor(seed);

// Billing reads the feed from the seed node, so it starts once the seed is an Up member.
billing.WithReference(seed).WaitFor(seed);

// The Device Gateway joins the same cluster with the role gateway: chargers connect to it at
// ws://localhost:5190/ocpp/{chargePointId}, its launch profile's address, and it reaches
// Charging's regions through proxies.
var gateway = builder.AddProject<Projects.Ohmline_DeviceGateway>("gateway")
    .WithHttpHealthCheck("/health/ready")
    .WithEnvironment("Akka__Remote__Port", Port(4060))
    .WithEnvironment("PetabridgeCmd__Port", Port(9120))
    .WithEnvironment("Akka__Cluster__SeedNodes__0", SeedNode)
    .WaitFor(seed);

// Chargers and drivers, started by hand from the dashboard: three chargers, a whole charge each.
builder.AddProject<Projects.Ohmline_Simulator>("simulator")
    .WithArgs("--chargers", "3", "--scenario", "plug-in-and-charge")
    .WithReference(gateway)
    .WithReference(seed)
    .WaitFor(gateway)
    .WaitFor(billing)
    .WithExplicitStart();

builder.Build().Run();

IResourceBuilder<ProjectResource> Node(string name, int index) =>
    builder.AddProject<Projects.Ohmline_Node>(name, launchProfileName: null)
        .WithHttpEndpoint(port: 5180 + index, name: "http")
        .WithHttpHealthCheck("/health/ready")
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("Akka__Remote__Port", Port(4053 + index))
        // pbm on 127.0.0.1:9110 for the seed, 9111 and 9112 for the others (section 18.8).
        .WithEnvironment("PetabridgeCmd__Port", Port(9110 + index))
        .WithEnvironment("Akka__Cluster__SeedNodes__0", SeedNode)
        .WithReference(ohmline)
        .WithReference(billing)
        .WithReference(fakeGrid)
        .WithEnvironment("Ohmline__SiteEnergy__GridOperator__Enabled", "true")
        .WithEnvironment("Ohmline__SiteEnergy__GridOperator__Sites__NG-VEN-0042", "slc-depot");

static string Port(int port) => port.ToString(CultureInfo.InvariantCulture);
