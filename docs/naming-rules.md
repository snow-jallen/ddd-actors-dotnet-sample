# Naming rules

Names in the sample come from `docs/glossary.md`. This page says how a glossary word becomes a
type, a member, a test or a stored string, and which names may never change once they are in
use. When a rule here and a name in the code disagree, the code is wrong.

## Messages

| Kind | Form | Ohmline examples | Not |
|---|---|---|---|
| Command | Imperative verb + glossary noun: what the sender wants | `ReserveConnector`, `StartSession`, `ApplyCurtailment` | `ConnectorReservationRequest`, `DoReserve` |
| Domain event | Glossary noun + past participle: the fact an expert would say | `ConnectorReserved`, `SessionEnded`, `CurtailmentApplied` | `ReserveConnectorEvent`, `OnReserved` |
| Reply | The outcome, in the past tense or as a noun | `TokenLocked`, `TokenLockRefused`, `CommandAccepted` | `ReserveConnectorResponse` |
| Domain error | The rule that was broken, as a noun phrase | `ConnectorUnavailable`, `ReservedForAnotherToken` | `Error42`, `InvalidOperation` |
| Integration event | Like a domain event, plus an explicit, versioned name on the wire | `SessionCompleted`, `ohmline.charging.session-completed.v1` | the domain event itself |

- **One command, one intention.** `ReserveConnector` and `CancelReservation` are two commands,
  not one `ChangeReservation` with a flag. A command that can mean two things leads to an event
  that records neither.
- **Events record what happened, not what changed in a table.** `ConnectorOccupied` says a car
  was plugged in. `ConnectorStatusChanged { Status = 2 }` says only that a column moved, and
  every reader has to work out why.
- **A command does not always produce the event with the matching name.** `RecordMeterValue`
  can produce `MeterAnomalyDetected`; `RequestAllocation` can produce `AllocationQueued`. Name
  each one for what it means, not to make them pair up.
- **No suffixes that repeat the kind.** Not `ReserveConnectorCommand` or `ConnectorReservedEvent`:
  the base type (`ChargePointCommand`, `ChargePointEvent`) already says it.

## Banned names

| Banned | Why | Instead |
|---|---|---|
| `Update*` (`UpdateConnector`) | Says that data changes, not what the business wants | The intention: `OccupyConnector`, `ReportConnectorFault` |
| `Set*` (`SetStatus`) | A setter sent as a message: the rules live with the sender, and the aggregate becomes anemic | The intention, or the fact that caused it |
| `*Changed` for a whole state (`ChargePointChanged`) | A state dump, not a fact; consumers have to diff | One event per fact |
| `*Manager` (`SessionManager`) | Names a job title, not a concept; collects unrelated rules | The domain noun: `ChargingSession`, `ChargingJourney` |
| `*Helper`, `*Util`, `*Utils` | Says nothing about what the code is for | A name for the concept: `FairShareAllocator`, `PriceCalculator` |
| A bare `Transaction` | Means three things in Ohmline (glossary) | `ChargingSession`, `Payment`, or a database transaction in infrastructure code only |

One exception: `SetChargingProfile` is the name of an action in the charger protocol, and the
Device Gateway keeps the external name so that it can be traced to the protocol.

"Process manager" is the name of a pattern, and it is fine in documents. The type that plays the
role is named after the process it manages: `ChargingJourney`, not `ChargingProcessManager`.

## Types and projects

- **Aggregates** are domain nouns: `ChargePoint`, `ChargingSession`, `SitePowerBudget`,
  `IdToken`. No `Aggregate`, `Entity` or `Model` suffix.
- **The actor that hosts an aggregate** is the aggregate's name plus `Actor`:
  `ChargePointActor`. It is the only kind of type that carries a technical suffix, because it
  is the technical shell around a domain concept.
- **Command and event bases** are the aggregate's name plus `Command` or `Event`:
  `ChargePointCommand`, `ChargePointEvent`. They live in one file each, named after the base.
- **Value objects** are glossary nouns in the singular: `Power`, `MeterReading`, `TokenUid`.
- **Projects and namespaces** are `Ohmline.<Context>.<Layer>`, with one folder per aggregate:
  `Ohmline.Charging.Contracts.ChargePoints`, `Ohmline.Charging.Domain.ChargePoints`.
- **Tests** are named `Given_<state>_when_<command>_then_<outcome>`, in glossary words:
  `Given_a_reserved_connector_when_another_token_occupies_it_then_it_is_refused`.

## Identifier formats

| Identifier | Format | Example |
|---|---|---|
| `SiteId` | lower-case kebab case, starting with a three-letter area code | `slc-depot` |
| `ChargePointId` | `OHM-` + three-letter area + `-` + four digits | `OHM-SLC-0042` |
| `ConnectorId` | charge point id + `/` + connector number, from 1 | `OHM-SLC-0042/1` |
| `TokenUid` | the card's hexadecimal UID, upper case | `04A2B3C4D5E6F7` |
| `SessionId`, `JourneyId` | Guid version 7, created from the `TimeProvider` | `GuidV7.New(clock)` |

## Durable names: chosen once, never changed

Most names can be refactored: rename a C# type, run the tests, done. A small set of names is
different, because it is **written into stored data or into another running system**. Changing
one of these after it has been used is a data migration, and getting it wrong loses data
silently or breaks a running cluster.

| Kind | Rule | Ohmline values |
|---|---|---|
| Shard type names | Kebab case, the aggregate's name | `charge-point`, `charge-point-listing`, `charging-session`, `charging-journey`, `site-power-budget`, `id-token` |
| Persistence ids | `{shard type name}-{entity id}` | `charge-point-OHM-SLC-0042`, `site-power-budget-slc-depot` |
| Number of shards | Fixed per region | `100` |
| Event manifests | `{context}.{kebab-type-name}.v{N}`; a new shape gets a new `N` | `charging.connector-reserved.v1` |
| Published names | `ohmline.{context}.{kebab-type-name}.v{N}` | `ohmline.charging.session-completed.v1` |
| Serializer ids | One per context, from the reserved range 9100–9199 | `9101` for Charging |
| Event tags | One per context or aggregate type | `charging`, `charging-session`, `site-energy` |
| Projection ids | Kebab case plus a version | `session-history-v1` |

The rules that follow from the table:

- **The manifest, not the C# type name, is what is stored.** Each context's serializer maps
  every event type to its manifest explicitly (section 10.3), so a C# rename is safe and a
  manifest rename is not.
- **A durable name is reviewed before its first commit,** with the same care as a database
  column. If it is wrong, add a new name next to the old one and migrate; never edit the old one
  in place (Chapter 20).
- **The glossary word wins.** A durable name uses the glossary's word at the time it is chosen.
  If the glossary later changes the word, the durable name stays, and the glossary records the
  old name as an alias.
