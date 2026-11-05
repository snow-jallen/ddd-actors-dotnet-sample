using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Ohmline.Charging.Actors.Journeys.Ports;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// One Ohmline node in memory, as <c>Program</c> builds it, ServiceDefaults and all, on the
/// test's PostgreSQL; its OpenTelemetry SDK exports every finished span into
/// <see cref="Exported"/> as well as wherever else it is configured to.
/// </summary>
public sealed class TracedNode(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    private readonly ConcurrentQueue<Activity> _exported = new();

    /// <summary>Every span the node's SDK has exported so far, from any trace.</summary>
    public IReadOnlyCollection<Activity> Exported => _exported;

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ohmline", postgres.ConnectionString)
            .UseSetting("Akka:Remote:Port", "0")
            // A node that has just started is slow over its first commands while every test
            // assembly runs at once: more than production's 5 s before the edge gives up.
            .UseSetting("Ohmline:Charging:AskTimeout", "00:00:20")
            .UseSetting("Ohmline:SiteEnergy:AskTimeout", "00:00:20")
            .UseSetting("Ohmline:DriverAccounts:AskTimeout", "00:00:20")
            .ConfigureTestServices(services => services
                .AddSingleton<IPaymentPort, InMemoryPaymentPort>()
                .AddSingleton<IChargerCommandPort, LoggingChargerCommandPort>()
                .ConfigureOpenTelemetryTracerProvider(tracing =>
                    tracing.AddInMemoryExporter(new QueueCollection(_exported))));

    /// <summary>The exporter's list, as a queue the test can read while it fills.</summary>
    private sealed class QueueCollection(ConcurrentQueue<Activity> queue) : ICollection<Activity>
    {
        public int Count => queue.Count;

        public bool IsReadOnly => false;

        public void Add(Activity item) => queue.Enqueue(item);

        public void Clear() => queue.Clear();

        public bool Contains(Activity item) => queue.Contains(item);

        public void CopyTo(Activity[] array, int arrayIndex) => queue.CopyTo(array, arrayIndex);

        public bool Remove(Activity item) => throw new NotSupportedException();

        public IEnumerator<Activity> GetEnumerator() => queue.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
