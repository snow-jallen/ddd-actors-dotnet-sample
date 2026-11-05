using System.Collections.Concurrent;
using System.Diagnostics;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// Logs that belong to their trace (section 18.4). Akka.NET writes a log later, from its logger
/// actor, on a thread where nothing is current; Akka.Hosting keeps the trace that was current
/// when the log was written, and <c>AddAkkaTraceCorrelation</c> puts it on the record. A log
/// written through <see cref="ILogger"/> in the handler needs no such help.
/// </summary>
public sealed class LogCorrelationTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_trace_correlation_then_an_actors_log_carries_the_trace_it_was_made_in()
    {
        using var spans = new SpanRecorder(ActorShellDiagnostics.Name);
        await using var node = await LoggingNode.StartAsync(correlate: true);
        using var request = new Activity("request").Start();

        var record = await node.SpeakAsync(request.TraceId);

        record.TraceId.ShouldBe(request.TraceId);
        var process = await spans.SingleAsync(request.TraceId, ActorShellDiagnostics.Name);
        record.SpanId.ShouldBe(process.SpanId);
    }

    [Fact]
    public async Task Given_no_trace_correlation_then_the_same_log_belongs_to_no_trace()
    {
        using var spans = new SpanRecorder(ActorShellDiagnostics.Name);
        await using var node = await LoggingNode.StartAsync(correlate: false);
        using var request = new Activity("request").Start();

        var record = await node.SpeakAsync(request.TraceId);

        record.TraceId.ShouldBe(default);
    }

    [Fact]
    public async Task Given_an_actors_log_then_its_category_is_the_actor_systems_not_the_actors()
    {
        using var spans = new SpanRecorder(ActorShellDiagnostics.Name);
        await using var node = await LoggingNode.StartAsync(correlate: true);
        using var request = new Activity("request").Start();

        var record = await node.SpeakAsync(request.TraceId);

        record.Category.ShouldBe("Akka.Actor.ActorSystem");
        record.Attributes["Who"].ShouldBe("OHM-SLC-0042");
        record.Attributes.ShouldContainKey("LogSource");
    }

    [Fact]
    public async Task Given_a_log_written_through_ILogger_in_the_handler_then_it_needs_no_help()
    {
        using var spans = new SpanRecorder(ActorShellDiagnostics.Name);
        await using var node = await LoggingNode.StartAsync(correlate: false);
        using var request = new Activity("request").Start();

        var record = await node.SpeakAsync(request.TraceId, throughILogger: true);

        record.TraceId.ShouldBe(request.TraceId);
        record.Category.ShouldBe("Ohmline.Telemetry.Tests.LogCorrelationTests.Speaker");
    }

    /// <summary>What a test needs of a log record, copied before the SDK reuses it.</summary>
    private sealed record Exported(
        ActivityTraceId TraceId,
        ActivitySpanId SpanId,
        string? Category,
        string? Message,
        IReadOnlyDictionary<string, object?> Attributes);

    /// <summary>A generic host with Akka.NET logging as the node configures it.</summary>
    private sealed class LoggingNode(IHost host, ConcurrentQueue<Exported> exported)
        : IAsyncDisposable
    {
        public static async Task<LoggingNode> StartAsync(bool correlate)
        {
            var exported = new ConcurrentQueue<Exported>();
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            if (correlate)
            {
                builder.Logging.AddOpenTelemetry(logging => logging.AddAkkaTraceCorrelation());
            }

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.AddProcessor(new Capture(exported));
            });
            builder.Services.AddAkka("ohmline", akka => akka
                .ConfigureLoggers(loggers =>
                {
                    loggers.ClearLoggers();
                    loggers.AddLoggerFactory();
                })
                .WithActors((system, registry, resolver) =>
                    registry.Register<Speaker>(
                        system.ActorOf(resolver.Props<Speaker>(), "speaker"))));
            var host = builder.Build();
            await host.StartAsync(Ct);
            return new LoggingNode(host, exported);
        }

        /// <summary>Asks the speaker, in the trace, to log; and waits for the record.</summary>
        public async Task<Exported> SpeakAsync(ActivityTraceId trace, bool throughILogger = false)
        {
            var speaker = await host.Services.GetRequiredService<IActorRegistry>()
                .GetAsync<Speaker>(Ct);
            var marker = Guid.NewGuid().ToString("N");
            await speaker.AskTraced<Spoken>(
                new Speak(marker, throughILogger), Patience, Ct);

            using var patience = new CancellationTokenSource(Patience);
            while (true)
            {
                if (exported.FirstOrDefault(record => record.Message?.Contains(marker) == true)
                    is { } record)
                {
                    return record;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(20), patience.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }
    }

    private sealed class Capture(ConcurrentQueue<Exported> exported) : BaseProcessor<LogRecord>
    {
        public override void OnEnd(LogRecord record)
        {
            var attributes = new Dictionary<string, object?>();
            foreach (var (key, value) in record.Attributes ?? [])
            {
                attributes[key] = value?.ToString();
            }

            exported.Enqueue(new Exported(
                record.TraceId,
                record.SpanId,
                record.CategoryName,
                record.FormattedMessage ?? record.Body,
                attributes));
        }
    }

    public sealed record Speak(string Marker, bool ThroughILogger);

    public sealed record Spoken;

    /// <summary>Logs in its handler, through Akka.NET or through ILogger, and answers.</summary>
    private sealed class Speaker : TracedReceiveActor
    {
        public Speaker(ILogger<Speaker> log)
        {
            var akkaLog = Context.GetLogger();
            Receive<Speak>(speak =>
            {
                if (speak.ThroughILogger)
                {
                    log.LogInformation("Spoken through ILogger ({Marker})", speak.Marker);
                }
                else
                {
                    akkaLog.Info(
                        "Speaking for {Who} ({Marker})", "OHM-SLC-0042", speak.Marker);
                }

                Sender.Tell(new Spoken());
            });
        }
    }
}
