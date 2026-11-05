# Bounded Context Canvas: Site Energy

*Bounded Context Canvas V5 (ddd-crew), filled in during section 5.1.*

## Name

**Site Energy**

## Purpose

Share each site's grid connection between the charging sessions at that site, so that the site
never draws more than it is allowed to and every car gets as much power as the site can safely
give it. This is the part of Ohmline that lets a site owner install more chargers than the grid
connection could run at full speed.

## Strategic classification

| Domain | Business model | Evolution |
|---|---|---|
| Core (site power allocation) | Revenue generator | Custom built |

## Domain roles

- **Enforcer:** the site's draw stays below its effective capacity, whatever any other context
  asks for.
- **Brain:** the fair-share allocation is the most rule-dense calculation in Ohmline, and
  Charging acts on its results without second-guessing them.

## Inbound communication

Every message in these tables is a public contract: it lives in a `Contracts` project, crosses a
serializer and changes only by adding a version.

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging (journey, subscribers) | `RequestAllocation`, `ReleaseAllocation` | Command | `Ohmline.SiteEnergy.Contracts` |
| Northgate Grid, through the grid operator ACL | `ApplyCurtailment`, `EndCurtailment` | Command | `Ohmline.SiteEnergy.Contracts` |
| AssetTrack, through the asset registry ACL; operators | `ConfigureSite` | Command | `Ohmline.SiteEnergy.Contracts` |
| Operators (HTTP) | A site's budget and allocations | Query | `Ohmline.SiteEnergy.Contracts` |

## Outbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging (journey) | Allocation granted, allocation queued | Event (reply) | `Ohmline.SiteEnergy.Contracts` |
| Charging (charging profile dispatcher, site live board) | Allocations rebalanced, allocation released | Event (by tag `site-energy`) | Read through the journal's tag, not a direct reference |

## Ubiquitous language

| Term | Meaning here |
|---|---|
| Site | One grid connection and therefore one power budget |
| Grid connection limit | The most power the site may draw from the grid |
| Safety margin | Headroom kept below the grid connection limit |
| Curtailment | A temporary reduction of capacity that the grid operator requested and Ohmline applied |
| Effective capacity | Grid connection limit minus active curtailment minus safety margin |
| Power allocation | Power granted to one connector's session, between its minimum and maximum |
| Priority class | Fleet or Public; fleet requests are served first |
| Rebalance (allocation) | Recomputing every allocation at a site after a change; never shard rebalancing |

Site Energy has no word for a charging session's meter readings, price or token. It sees a
session only as a request for power on a connector.

## Business decisions

- The sum of allocations at a site never exceeds its effective capacity.
- Every allocation lies between its connector's minimum and maximum power; a request that cannot
  get its minimum is queued, not given less.
- Fleet requests are served before public ones; within a class, capacity is shared fairly
  (water-filling) up to each connector's maximum.
- A car arriving, a car leaving, or a curtailment starting or ending rebalances every allocation
  at the site.
- A curtailment is applied as requested; Site Energy never argues with the grid operator.

## Assumptions

- A site has at most a few hundred connectors, so one budget per site can decide fast enough
  (measured in section 15.8).
- Chargers apply a new charging profile within a few seconds of receiving it.
- The asset registry's numbers are wrong often enough that every site configuration is checked
  before it is accepted.

## Verification metrics

- Zero minutes in which a site's measured draw exceeds its effective capacity.
- A rebalance reaches every affected charger within 5 s.
- The share of requests that are queued, per site and hour, is reported to the site owner.

## Open questions

- How should a fleet contract's guaranteed minimum interact with a curtailment that cannot honour
  it?
- Does the depot with 300 connectors need its budget split by feeder (section 15.8)?
