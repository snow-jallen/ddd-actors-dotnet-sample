using Akka.Hosting;
using Akka.Persistence.Hosting;

namespace Ohmline.ActorShell.Tracing;

/// <summary>What an actor system needs to send, receive and store trace envelopes.</summary>
public static class TracingAkkaExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// The envelope's serializer, bound to <see cref="TraceEnvelope"/>. Every node of the
        /// cluster registers it (<c>WithOhmlineCluster</c>), since any of them may be sent one.
        /// </summary>
        public AkkaConfigurationBuilder WithTraceEnvelopes() =>
            akka.WithCustomSerializer(
                TraceEnvelopeSerializer.Name,
                [typeof(TraceEnvelope)],
                system => new TraceEnvelopeSerializer(system));
    }

    extension(AkkaPersistenceJournalBuilder journal)
    {
        /// <summary>
        /// Tags traced events as their contexts' taggers would (section 17.7): the host's one
        /// <typeparamref name="TTagger"/>, bound to <see cref="TraceEnvelope"/>.
        /// </summary>
        public AkkaPersistenceJournalBuilder AddTraceEnvelopeTagger<TTagger>()
            where TTagger : TraceEnvelopeTagger =>
            journal.AddWriteEventAdapter<TTagger>(
                TraceEnvelopeTagger.Name, [typeof(TraceEnvelope)]);
    }
}
