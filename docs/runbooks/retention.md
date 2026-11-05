# Runbook: retention, deletion and personal data

Ohmline's journal is append-only, and the default is to **keep every event for ever** (section
20.10). Events are the system of record: projections are rebuilt from them (section 20.9), a
session's price is explained by them, and a regulator's or a driver's question about last year is
answered from them. This runbook says what is stored, what may be deleted, and how, for the rare
case where something must go.

## What the events hold, and what they do not

Events identify a driver by two values only:

| Value | What it is | Where the person is |
|---|---|---|
| `TokenUid` | The RFID card's or the app credential's id, such as `04A2B3C4D5E6F7` | Nowhere in Charging; Driver Accounts maps it to a `DriverRef` |
| `DriverRef` | An opaque reference, such as `drv-7f3a91c2` | In the driver account system outside the cluster, which can delete the person |

No event holds a name, an e-mail address, a phone number or a postal address.
`PersonalDataTests` in `Ohmline.ArchitectureTests` fails the build if a property of any event,
however deep in its values, is named like one. Billing's tables hold `driver_ref` and amounts,
never card data: TallyPay holds the card (section 16.4).

When a driver asks to be forgotten, the person is deleted where the person is: the account that
`DriverRef` points to. The events stay, and now point at nobody. A `TokenUid` printed on a card
the driver no longer has is not, on its own, personal data; if counsel decides otherwise for a
market, see crypto-shredding below.

> **Crypto-shredding** (not implemented in the sample) keeps a per-driver key in a store that can
> delete it, and writes only ciphertext of the personal fields into events. Deleting the key
> erases the data in every event and every backup at once, without touching the journal. It costs
> a key lookup on every read and makes projections that need the plain value harder to rebuild,
> so it is worth it only for data that must be in events and must be erasable.

## What "deleted" means in Akka.Persistence.Sql

- `DeleteMessages(toSequenceNr)` from an actor marks rows `deleted = true` and physically deletes
  older ones, but **keeps the last row with its payload**, so the journal still knows the highest
  sequence number (section 11.7). "Deleted" is not "erased".
- Only the state-sourced `ChargePointListing` deletes its events, after each snapshot.
- Since section 20.6, recovery that finds no readable snapshot and a journal that no longer starts
  at sequence number 1 stops the aggregate instead of starting from part of its history. Deleting
  events below a snapshot is therefore safe only while that snapshot can be read.

## If events must be purged

Purging is a decision with an owner, a reason and a date, recorded in an ADR, never a script run
in an incident. If it is decided:

1. **Pick whole aggregates that are over**, such as charging sessions that ended more than N
   years ago and whose invoices are settled, never part of a live aggregate's history.
2. **Stop the projections that read them**, or accept that a rebuild (section 20.9) can no longer
   reproduce those rows; keep the read model rows if they are still needed.
3. **In one database transaction**, delete the aggregate's `tags` rows and its `journal` rows,
   and its `snapshot` rows. Never leave a tag row whose event is gone, or an event whose tags
   are gone.
4. **Keep the highest sequence number** of any aggregate that may receive another command: leave
   its last row, marked `deleted`, as `DeleteMessages` does. Deleting it would let the next
   command reuse sequence number 1 and collide with what projections have already read.
5. **Never touch `ordering`**: projections store it as their offset, and the integration
   outbox's and the driver app feed's positions are consumers' offsets too.
6. **Back up first**, and run the restore drill (`backup-restore.md`) on the backup before the
   purge runs in production.

A sketch of step 3 for one ended session, to adapt and review, never to run as it is:

```sql
BEGIN;
DELETE FROM tags     WHERE persistence_id = 'charging-session-<id>';
DELETE FROM snapshot WHERE persistence_id = 'charging-session-<id>';
DELETE FROM journal  WHERE persistence_id = 'charging-session-<id>';
COMMIT;
```

The session will never receive another command (its id is never reused), so step 4 does not
apply to it; for any other aggregate it does.

## What is never deleted

The manifests' readers (the golden corpus, section 20.5), the serializer ids, and the published
positions of the integration outbox. A consumer that reads the feed from position 0 must find
every position it ever found, or a gap it can be told about.
