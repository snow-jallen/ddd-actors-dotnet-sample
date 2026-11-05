using Akka.Actor;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;

namespace Ohmline.Charging.ReadModels.Query;

/// <summary>
/// The read side of the journal: Akka.Persistence.Sql's read journal, a stream API over the
/// same tables the actors write (section 13.3). It answers questions about events, never about
/// state: which events one aggregate has, which events carry a tag, which aggregates exist.
/// </summary>
public static class ReadJournalAccess
{
    extension(ActorSystem system)
    {
        /// <summary>
        /// The node's <see cref="SqlReadJournal"/>, configured from
        /// <c>akka.persistence.query.journal.sql</c>. Persistence Query makes one per system and
        /// hands out the same one on every call.
        /// </summary>
        public SqlReadJournal ReadJournal =>
            PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
    }
}
