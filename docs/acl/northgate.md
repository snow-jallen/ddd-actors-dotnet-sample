# Anticorruption layer: Northgate Grid

Northgate Grid is the (fictional) grid operator that may ask Ohmline's sites to draw less for a
while (section 16.8). It publishes demand-response events in the style of OpenADR 2.0b; Site
Energy knows only curtailments.

| Part | Where |
|---|---|
| Northgate's shapes (`NorthgateSignals`, `DemandResponseEvent`) and `NorthgateClient` | `src/SiteEnergy/Ohmline.SiteEnergy.Acl.GridOperator/Northgate/` |
| `GridSignalTranslator` (pure) and `GridInstruction` (`Curtail`, `Restore`) | `src/SiteEnergy/Ohmline.SiteEnergy.Acl.GridOperator/` |
| `GridSignalPoller`, the cluster singleton `grid-signal-poller` | same |
| Stand-in: `GET /signals?since=`, `POST /signals` | `tools/Ohmline.FakeGridOperator` |
| Settings: `Ohmline:SiteEnergy:GridOperator` (`Enabled`, `PollInterval`, `Sites`) | the node |

## The translation

| Northgate | Site Energy |
|---|---|
| VEN id (`NG-VEN-0042`) | `SiteId`, by the `Sites` table in configuration; a VEN not in it is none of Ohmline's |
| An event in force: not `cancelled` or `completed`, and now within `dtstart` + `duration` (ISO 8601, `PT2H`) | `ApplyCurtailment(site, reduction)` |
| `LOAD_DISPATCH` `delta` payload, negative, in `kW` (older systems: `W`, `MW`) | `Reduction` in kW, as a positive `Power` |
| Several events in force for one site | the deepest cut |
| No event in force any more | `EndCurtailment(site)` |
| An event revision (`eventID`, `modificationNumber`) | the command's id, derived from it, so the same revision is always the same command |

The poller tells a site only when what it should do changes, and remembers that in memory: a
poller that takes over reads the feed from the start and tells every site once. Site Energy's
budget ignores a curtailment it already has and an end when none is in force.

## From a curtailment to the chargers

A curtailment rebalances the site's allocations (section 7.8). The projection
`charging-profile-dispatcher-v1` reads every allocation event on the `site-energy` tag and sends
each connector its new limit as a `SetChargingProfile` through the Device Gateway, with the site
budget's version as the profile's id. The gateway sends a charger an allocation once and never an
older one. A charger that is not connected cannot be told: after the port's attempts the event is
retried, then recorded in `read_models.projection_failures` and skipped, and the charger gets
its limit with the site's next allocation.
