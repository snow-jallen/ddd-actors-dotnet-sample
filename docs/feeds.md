# The charging feed

Charging publishes what other services need to know through one **Open Host Service**: an HTTP
feed of its **Published Language**, the integration events in
`src/Charging/Ohmline.Charging.Contracts/Integration/` (section 16.2). Every consumer reads the
same feed in the same way; Charging builds no integration for any one of them, and keeps no list
of who reads.

## Where the events come from

```text
session actor ──persist──> journal ──IntegrationEventRelay──> read_models.integration_outbox ──> GET /feeds/charging
```

Charging stores its domain events in the journal and nothing else: the journal is the outbox
(section 16.1). The projection `integration-relay-v1` translates the events other services care
about into integration events and writes them to `read_models.integration_outbox`, in the same
database transaction as its offset. Each row gets the next number of the table's sequence: its
**position**.

## Reading it

```http
GET /feeds/charging?after=0&limit=100
```

| Parameter | Meaning | Default |
|---|---|---|
| `after` | The position of the last event the consumer has handled; 0 to read from the start | 0 |
| `limit` | The most events in the page, from 1 to 500 | 100 |

```json
{
  "entries": [
    {
      "position": 17,
      "event": {
        "type": "ohmline.charging.session-completed.v1",
        "sessionId": "019a5a52-7b40-7c66-9a51-2f1c8d1e4b07",
        "tokenUid": "04A2B3C4D5E6F7",
        "connectorId": "OHM-SLC-0042/1",
        "energyKwh": 30,
        "price": {
          "currency": "USD",
          "total": 12.60,
          "lines": [
            { "code": "energy", "quantity": 30, "unit": "kWh", "unitPrice": 0.42, "amount": 12.60 }
          ]
        },
        "startedAt": "2026-11-04T14:40:00+00:00",
        "endedAt": "2026-11-04T15:25:00+00:00",
        "eventId": "019a5a52-7b40-7d0e-8f3b-55a1e2c06a9d",
        "occurredAt": "2026-11-04T15:25:00+00:00"
      }
    }
  ],
  "next": "/feeds/charging?after=17&limit=100"
}
```

A consumer:

1. asks for the page after the position it stored last;
2. handles each entry, in order, and stores the entry's position **in the same database
   transaction** as what it did with the event (an idempotent receiver, section 16.3);
3. follows `next`. At the end of the feed, `next` asks the same question again and the page is
   empty until something new is published, so a consumer polls `next` with a pause.

## Guarantees

- **Order.** Positions only grow, and one relay writes the outbox, one event at a time: an event
  never appears below a position a consumer has already read past. Positions may have gaps (a
  rolled-back write uses up its number); a consumer never waits for one to be filled.
- **At least once.** A consumer that crashes after handling an event and before storing its
  position reads it again. Every event has an `eventId`, the same every time the same fact is
  published; `SessionCompleted` is also unique by `sessionId`.
- **Tolerant reading.** Each event carries its `type`. A consumer deserializes the types it knows
  and steps over the rest, storing their positions too; a new kind of event, or a `.v2` of an
  existing one, never stops an old consumer. Unknown fields are ignored.
- **Forever.** The feed can be read from position 0 at any time, by a new consumer or to rebuild
  one. Every version ever published stays readable (section 20.9).

## Caching

| Header | Value |
|---|---|
| `ETag` | `"{after}-{last position in the page}"`: the page's content, exactly |
| `Cache-Control` | `public, max-age=31536000, immutable` for a full page, which can never change again; `no-cache` for a page with room left |

A consumer that sends `If-None-Match` with the ETag it got gets `304 Not Modified` while nothing
new has been published. A full page can be served by any HTTP cache in front of the nodes.

## The versions published

| Wire name | Type | Since | Consumers |
|---|---|---|---|
| `ohmline.charging.session-completed.v1` | `SessionCompleted` | 16.1 | Billing |
| `ohmline.charging.session-completed.v2` | `SessionCompletedV2` | 20.9 | none yet |

Golden JSON files for each version are in `tests/golden/published-language/`, checked by
`tests/Ohmline.ContractTests` (section 16.10).

### Publishing a new version (section 20.9)

A shape that changes is a new wire name, published **beside** the old one: the relay writes a
completed session as `.v1` and, at the next position, as `.v2`, each with an event id of its
own, so a consumer that reads both can tell them apart and one that reads one is not confused by
the other. Consumers move to `.v2` at their own pace. Each consumer says which versions it reads
(Billing: `ChargingFeedReader.WireNamesRead`), and `BillingReadsTheFeedTests` fails if Charging
stops publishing one of them; only when no consumer reads `.v1` any more does the relay stop
writing it. The positions and the rows already written stay, for ever.

## Why a pull feed and not a broker

A feed over HTTP needs nothing Ohmline does not already run, can be read from the start by
anyone at any time, and makes each consumer responsible for its own position. A message broker
pushes, scales out to many consumers better, and is the usual choice once a dozen services
read the same events. With Kafka the relay would stay the same and write to a topic instead of
a table: Akka.Streams.Kafka offers a producer sink that fits after the projection runner. The
sample does not include it; section 16.2 discusses the trade.
