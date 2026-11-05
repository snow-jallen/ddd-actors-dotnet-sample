# Ohmline glossary

This is the ubiquitous language of Ohmline. Use these words exactly as written here: in
conversations, in documents, in type and member names, and in test names. When a conversation
produces a better word, change the glossary first and the code in the same commit.

The glossary starts with the core words from the domain primer (`docs/ohmline-primer.md`) and
grows with the book.

| Term | Context | Meaning |
|---|---|---|
| Site | Site Energy, Charging | A location with one grid connection and therefore one power budget |
| Grid connection limit | Site Energy | The maximum power the site may draw from the grid |
| Safety margin | Site Energy | Headroom Ohmline keeps below the grid connection limit |
| Curtailment | Site Energy | A temporary reduction of the site's capacity, requested by the grid operator |
| Effective capacity | Site Energy | Grid connection limit minus active curtailment minus safety margin |
| Power allocation | Site Energy | Power granted to one connector's session; always between the connector's minimum and maximum |
| Charge point (charger) | Charging | A physical charging device with one or more connectors |
| Connector | Charging | A numbered plug on a charge point, written `OHM-SLC-0042/1`; hosts at most one session at a time |
| Id token (token) | Driver Accounts, Charging | An RFID card or app credential that authorizes charging |
| Charging session (session) | Charging | One charge on one connector, from start to end. OCPP calls it a "transaction"; inside Ohmline it is always a session |
| Meter reading | Charging | The charge point's cumulative energy register (Wh) with a timestamp; it never decreases |
| Tariff snapshot | Tariffs, Charging | The version of the site's tariff copied into a session when it starts; later tariff changes never affect that session |
| Hold (pre-authorization) | Billing | Money reserved on the driver's card before charging; later captured for the final price or voided |
