# Ohmline.Charging.Contracts

This project is Charging's public face: every message another part of Ohmline may send to
Charging, or receive from it. Nothing else in Charging may be referenced from outside the
context. It references the shared kernel and the contracts of the contexts it depends on
(Tariffs, whose tariff snapshot a session starts with, section 8.2), and nothing else, which
`tests/Ohmline.ArchitectureTests` checks.

It has two audiences, and two sets of rules.

| Folder | Audience | Wire name | Section |
|---|---|---|---|
| `ChargePoints/`, `Sessions/`, `Listings/`, ... | Senders inside the cluster: the Device Gateway, the HTTP edge, the charging journey | Internal manifest `charging.{kebab-type-name}.v{N}` | 7.2, 10.3 |
| `Integration/` | Every other service: Billing first, Roaming and others later. This is Charging's **Published Language** | `ohmline.charging.{kebab-type-name}.v{N}` | 16.1 |

Senders inside the cluster address an aggregate by the id its command carries, through a shard
region (section 12.2). Each folder therefore also holds the key under which a node's actor
registry keeps that region (`ChargePointRegion`, `ChargingSessionRegion`,
`ChargePointListingRegion`, `ChargingJourneyRegion`), so that a sender needs these contracts and
no actor.

Readers inside the node ask the journal for Charging's events by tag (section 13.2).
`ChargingTags` names the tags, so a projection needs these contracts and not the actors that
write them. Tags are as durable as wire names: events keep the tag they were stored with.

The Published Language is served by an **Open Host Service**, the charging feed
(`GET /feeds/charging`, section 16.2): one protocol for every consumer, instead of an
integration built for each.

## Naming

- **Commands** are imperative (`ReserveConnector`), **replies** name the outcome
  (`CommandAccepted`), **integration events** are past tense (`SessionCompleted`). The glossary
  word wins (`docs/naming-rules.md`).
- **The wire name is explicit and is never derived from the C# name.** A C# type may be renamed
  or moved; its wire name may not. Integration events declare theirs with
  `[JsonDerivedType(typeof(SessionCompleted), "ohmline.charging.session-completed.v1")]`.
- **The context is part of the published name** (`ohmline.charging.`), so a consumer that reads
  several feeds never confuses two contexts' events.
- **One message family per closed hierarchy**: a `closed record` base with `sealed record`
  leaves (ADR 0003). Never a union, which does not survive serialization.
- **Fields use the shared kernel's types or plain values with the unit in the name:** energy as
  kWh in a `decimal` (`EnergyKwh`), money as an amount and an ISO currency code, instants as
  `DateTimeOffset`, identifiers in their printed form (`OHM-SLC-0042/1`). An integration event
  never contains a type from `Ohmline.Charging.Domain`.

## Versioning

- **Versions belong to messages, not to this project.** `session-completed.v1` and
  `session-completed.v2` can live side by side; nothing else in the project changes because one
  message got a new version.
- **Adding an optional field is not a new version.** Consumers are tolerant readers: they ignore
  fields they do not know, and a missing new field means "not provided".
- **Anything else is a new version:** removing or renaming a field, changing a unit, a meaning
  or a type, or making an optional field required. The new version is a new type
  (`SessionCompletedV2`) with a new name (`.v2`).
- **Expand, then contract.** Publish the new version alongside the old one, wait until every
  consumer has moved (the context map lists them), then stop publishing the old one. A
  published name is never reused, and consumers must be able to read every version ever
  published, because the feed can be replayed from the start (section 20.9).
- **Every version has a golden JSON file** (`tests/Ohmline.ContractTests`, section 16.10). A
  change that alters an existing file fails the build.

## Reviews

`.github/CODEOWNERS` makes the review group `charging-contracts@ohmline.example` the owner of
this folder: the Sessions team, which supplies these messages, and the teams that consume them
(Gateway and Finance IT). A pull request that changes a message waits for one approval from
each.
