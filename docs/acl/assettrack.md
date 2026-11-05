# Anticorruption layer: AssetTrack

AssetTrack is the legacy asset registry that Ohmline bought years ago and cannot replace yet. It
knows every site and every charge point, and it is the only record of each site's grid
connection. It is also a Big Ball of Mud: free-text fields, units typed by hand, the same site
spelled three ways, and nobody who still knows why some columns exist.

Site Energy needs one thing from it: a correct `SiteConfiguration` for every site (grid
connection limit, safety margin, time zone, and each connector's minimum and maximum power and
priority class). This page is the contract for the translator that produces it,
`Ohmline.SiteEnergy.Acl.AssetRegistry` (section 6.7). Its input is the nightly CSV export; a
sample is committed as `tools/legacy-assets/assettrack-export.csv`.

## The boundary

- **The translator is a pure function** from the export's rows to outcomes. It reads no clock,
  calls nothing and changes nothing in AssetTrack.
- **AssetTrack's words stop here.** No AssetTrack column name, status or identifier appears in
  Site Energy's model or messages. `ASSET_ID` is kept only to tell a person which row to fix.
- **The translator never guesses.** A value it cannot read with certainty is rejected and
  reported, so that the asset team fixes it in AssetTrack, where everyone else reads it too. A
  guess about a safety margin or a priority class would put a wrong number into the power
  budget, silently.

Every row has exactly one outcome:

| Outcome | Meaning | What happens |
|---|---|---|
| Translated | The row describes an active connector, and every field was read with certainty | It becomes part of its site's `SiteConfiguration` |
| Skipped | The row is not meant for Site Energy: retired, a test record or an exact duplicate | Counted, not reported |
| Rejected | The row is meant for Site Energy but cannot be trusted | Reported with the line number, `ASSET_ID` and the reason; left out of the configuration |

A site's configuration contains its translated connectors. A site whose rows are all rejected
or skipped gets no configuration, and Site Energy keeps the one it had.

## The export

One row per connector. The site's fields are repeated on every row, which is how they come to
disagree.

| Column | Example | Becomes |
|---|---|---|
| `ASSET_ID` | `AT-10041` | Nothing; quoted in rejection reports only |
| `SITE_CODE` | `SLC-DEPOT`, `slc depot`, `SLC_DEPOT` | `SiteId` |
| `SITE_NAME` | `"Salt Lake Depot, Bays 1-6"` | Nothing (the listing has its own, section 11.7) |
| `GRID_CONN` | `200 kW`, `200000W`, `0.4 MW` | Grid connection limit (`Power`) |
| `MARGIN` | `10 kW`, `7500 W` | Safety margin (`Power`) |
| `TZ` | `America/Denver`, `Mountain` | The site's time zone id |
| `CHARGER_SERIAL` | `OHM-SLC-0041`, `OHMSLC0043` | `ChargePointId` |
| `PLUG_NO` | `1`, `#2` | The connector number in `ConnectorId` |
| `MIN_PWR`, `MAX_PWR` | `6 kW`, `6000`, `22kW` | The connector's minimum and maximum power (`Power`) |
| `PRIO` | `FLEET`, `P`, blank | `PriorityClass` (Fleet or Public) |
| `STATUS` | `ACTIVE`, `DECOMMISSIONED`, `TEST` | Whether the row is translated at all |
| `LAST_EDIT` | `2025-02-11`, `02/11/2025` | Nothing |

## Translation rules

The rules run in this order. The first rule that rejects or skips a row decides its outcome.

1. **Status.** Trimmed and compared without regard to case. `ACTIVE` continues;
   `DECOMMISSIONED` and `RETIRED` are skipped; `TEST` is skipped; anything else, including a
   misspelling such as `Decommisioned`, is rejected.
2. **Site code.** Trim, lower-case, and turn every run of spaces and underscores into one
   hyphen. The result must look like a site id: three letters, then one or more hyphenated
   parts (`slc-depot`). `SLC-DEPOT`, `slc depot` and `SLC_DEPOT` are the same site.
3. **Power values** (grid connection, margin, minimum, maximum). A number with a `.` as the
   decimal point, optionally followed by a unit, with or without a space: `W`, `kW` or `MW` in
   any case. **A number without a unit is in watts**, AssetTrack's own unit for these columns;
   people typing kilowatts added the unit. A comma (`"3,7 kW"`) is rejected: it may be a decimal
   comma or a thousands separator, and the translator does not guess which.
4. **Plausibility.** A grid connection limit between 10 kW and 10 MW; a safety margin of zero or
   more and below the grid connection limit; a connector's minimum and maximum each between
   1 kW and 400 kW, and the minimum no larger than the maximum. This is what catches a bare `11`
   that someone meant as 11 kW: as 11 W, it is implausible, so it is rejected rather than read
   as either.
5. **Time zone.** An IANA time zone id, compared without regard to case. The aliases that
   AssetTrack's drop-down used to offer, `Mountain`, `US/Mountain` and `MST7MDT`, become
   `America/Denver`. Anything else, including a misspelt id, is rejected.
6. **Charge point.** Trim and upper-case. `OHMSLC0043` gets its hyphens back
   (`OHM-SLC-0043`). The result must be a charge point id (`docs/naming-rules.md`); a blank
   serial is rejected.
7. **Connector number.** Trim and drop a leading `#`. It must be a whole number from 1.
8. **Priority class.** `FLEET` and `F` are Fleet; `PUBLIC` and `P` are Public, in any case.
   **A blank priority is Public**: fleet precedence is a contract, so it has to be stated, and
   leaving it out can only cost a fleet customer, never another driver. A misspelling such as
   `Pubic` or `Flet` is rejected: a translator that corrects one typo will one day correct the
   next one wrongly, and then a public driver is served before a fleet.
9. **Duplicates.** Rows that translate to the same connector (`OHM-SLC-0043/1`) and the same
   values are one row: the first is translated and the others are skipped. If they disagree on
   any value, every row for that connector is rejected.
10. **Site agreement.** All translated rows of one site must agree on the grid connection limit,
    the safety margin and the time zone, after conversion (`200 kW`, `200000W` and `0.2 MW`
    agree). If they do not, every row of the site is rejected: the translator cannot know which
    number is the grid connection Ohmline pays for.
11. **Effective capacity.** A connector's maximum may not exceed the site's grid connection limit
    minus its safety margin.

`SITE_NAME`, `LAST_EDIT` and `ASSET_ID` are never used to decide anything. In particular, a
newer `LAST_EDIT` does not win a disagreement: the dates are in two formats and are changed by
any edit, including a typo fix in the site name.

## The committed export, translated

`tools/legacy-assets/assettrack-export.csv` has 40 rows (lines 2 to 41). Every rule above is
exercised at least once, and the tests in section 6.7 expect exactly this result: **19
translated, 5 skipped, 16 rejected**, and four site configurations.

| Line | Outcome | Rule | Why |
|---|---|---|---|
| 2, 3, 5, 6, 7 | Translated | — | `slc-depot`: 200 kW, 10 kW margin, connectors `0041/1`, `0041/2`, `0042/2`, `0043/1`, `0043/2`. Line 3 is in watts; line 5 uses `Mountain`, a lower-case serial and `#2`; line 6 has no hyphens; line 7 has a blank priority and is Public |
| 4 | Rejected | 5 | Time zone `America/Denvr` |
| 8 | Skipped | 9 | The same connector and values as line 6 |
| 9, 10, 11, 13, 14, 16 | Translated | — | `slc-west-garage`: 150 kW, 7.5 kW margin (line 11 writes it as `7500 W`), six Public connectors |
| 12 | Rejected | 4 | Minimum 22 kW above maximum 7.4 kW |
| 15 | Rejected | 8 | Priority `Pubic` |
| 17, 18, 41 | Translated | — | `ogd-library`: 60 kW, 5 kW margin; line 18 uses `US/Mountain` |
| 19 | Rejected | 4 | Maximum `11`, read as 11 W |
| 20 | Rejected | 3 | Minimum `"3,7 kW"` |
| 21, 26, 28 | Skipped | 1 | Decommissioned or retired |
| 22–25 | Rejected | 10 | `pvo-center` rows say 120 kW (line 25 as `120000 W`) and 150 kW |
| 27 | Rejected | 1 | Status `Decommisioned` |
| 29 | Skipped | 1 | A test record |
| 30, 31, 33, 35, 38 | Translated | — | `slc-fleet-yard`: 400 kW (line 30 as `0.4 MW`), 20 kW margin, Fleet |
| 32 | Rejected | 8 | Priority `Flet` |
| 34 | Rejected | 4 | Maximum 500 kW |
| 36, 37 | Rejected | 9 | Two rows for `OHM-SLC-0304/1` with maxima of 150 kW and 120 kW |
| 39 | Rejected | 6 | No charger serial |
| 40 | Rejected | 7 | Connector number 0 |

## The other Big Ball of Mud

The charge points are the second one. Every vendor's firmware speaks its own dialect of the
protocol: energy in Wh or kWh, its own status names, fields that appear only in some versions.
The same rules apply there, and the Device Gateway is that context's anticorruption layer
(section 16.6): a pure translator, outcomes instead of exceptions, and recorded frames from each
vendor as test fixtures.
