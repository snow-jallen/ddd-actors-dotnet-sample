using Akka.Persistence.Journal;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// Tags a traced event as its context's tagger would tag the event inside (section 17.7). The
/// journal picks one write adapter by the type of what was persisted, so a context's tagger,
/// bound to its own event families, never sees a <see cref="TraceEnvelope"/>, and the event
/// would reach the journal without its tags: invisible to every projection. A host binds one
/// subclass of this to <see cref="TraceEnvelope"/>, listing every context's tagger.
/// </summary>
/// <param name="taggers">The contexts' taggers; the first that tags the event wins.</param>
public abstract class TraceEnvelopeTagger(params IReadOnlyList<IWriteEventAdapter> taggers)
    : IWriteEventAdapter
{
    /// <summary>The adapter's name in the journal's configuration.</summary>
    public const string Name = "trace-envelope-tagger";

    /// <summary>No manifest of its own: the envelope serializer's is the one that counts.</summary>
    public string Manifest(object evt) => string.Empty;

    /// <summary>
    /// The envelope, whole, with the tags the event inside it would have had; untagged if no
    /// tagger tags that event. (<see cref="Tagged"/> is a struct: a LINQ FirstOrDefault would
    /// give a default one, with null tags, which the SQL journal fails to write.)
    /// </summary>
    public object ToJournal(object evt)
    {
        if (evt is TraceEnvelope envelope)
        {
            foreach (var tagger in taggers)
            {
                if (tagger.ToJournal(envelope.Message) is Tagged tagged)
                {
                    return new Tagged(envelope, tagged.Tags);
                }
            }
        }

        return evt;
    }
}
