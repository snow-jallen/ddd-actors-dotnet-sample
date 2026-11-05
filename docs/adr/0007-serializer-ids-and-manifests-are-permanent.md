# 0007. Serializer ids and manifests are permanent

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch10-s03

## Context

From section 10.4, every decision a Charging aggregate makes is written to the journal, and the
journal is never rewritten: a `ConnectorReserved` recorded at the bus depot today is replayed by
every version of Ohmline that ever recovers that charge point. The same bytes also travel between
cluster nodes, possibly between two versions of the code during a rolling update.

Akka.NET stores three things beside each event: the **identifier** of the serializer that wrote
it, a **manifest** that tells that serializer what it is reading, and the bytes. What goes into
the last two depends on the serializer:

- **Akka.NET 1.5's default for user types is Newtonsoft.Json** (identifier 1). It leaves the
  manifest empty and writes the CLR type into the payload:
  `"$type":"Ohmline.Charging.Domain.ChargePoints.ConnectorReserved, Ohmline.Charging.Domain"`.
  Renaming the type, moving it to another namespace or project, or splitting an assembly makes
  every stored event unreadable, and a value object whose only constructor is private cannot be
  rebuilt at all. Section 3.9 also showed that it restores a union with `Value == null`, silently.
- **A `SerializerWithStringManifest`** writes whatever manifest we choose. The type name is no
  longer in the data; the manifest is, and it is ours.

Both the identifier and the manifest therefore become part of the stored data. Changing either
one after an event has been written is the same as deleting that event.

## Decision

We will serialize everything a bounded context tells, asks, replies or persists with one
`ManifestJsonSerializer` subclass per context (`Ohmline.ActorShell/Serialization`):
System.Text.Json, camel-case properties, value objects written as their raw value by
`ValueObjectConverter`, and a manifest per type taken from a registry that is written out by
hand, never derived from a type name at run time. Each serializer is registered once, with
`WithCustomSerializer`, and bound to the closed-record base types of its context.

We will never change or reuse these ids:

| Identifier | Serializer | From |
|---|---|---|
| 9100 | `TraceEnvelopeSerializer` | section 17.4 |
| 9101 | `ChargingSerializer` | section 10.3 |
| 9102 | `SiteEnergySerializer` | section 10.10 |
| 9103 | `DriverAccountsSerializer` | section 12.7 |
| 9104 | `TariffsSerializer` | section 12.8 |
| 9105 to 9199 | reserved for Ohmline; anything outside this range is not ours | |

We will name manifests `{context}.{kebab-type-name}.v{N}`, with the contexts `charging`,
`site-energy`, `accounts` and `tariffs` (for example `charging.connector-reserved.v1`), and
snapshots `{context}.{aggregate}-snapshot.v{N}`. A manifest, once released, is never changed,
never removed and never given to another type:

- **Renaming or moving a C# type** changes nothing on disk: its registry line keeps the manifest.
- **Adding an optional field** keeps the manifest. The reader is tolerant: unknown fields are
  ignored, and a missing optional field takes its default.
- **Any other change of shape** (removing or renaming a field, changing a unit, a meaning or a
  type, or making a field required) is a new manifest version (`.v2`). The serializer keeps
  reading `.v1` for ever, upcasting it to the current type (section 20.3).
- **An unknown manifest** throws a `SerializationException`, which Akka.NET treats as a message
  it cannot read rather than a broken connection.

We will keep a golden JSON file for every manifest under `tests/golden/`. A test writes each
message, compares the JSON with its golden file, and reads the golden file back. A change to a
golden file that has already been released is refused in review.

## Consequences

- Every new message type costs one line in its context's registry and one golden file, and a
  test refuses a bound type without a manifest (`SerializationTests`), so neither can be
  forgotten.
- C# types can be refactored freely; the journal no longer depends on namespaces or assemblies.
- The rules that stored data must follow become reviewable: a pull request that alters a golden
  file is a pull request that changes stored data.
- A shape change takes two steps in a running cluster: first deploy a version that can read the
  new manifest, then one that writes it (section 20.2).
- `SerializationTests` in each context's actor tests and `ManifestJsonSerializerTests` in
  `Ohmline.ActorShell.Tests` enforce the registry, the scheme, the golden files and the
  exception on unknown manifests.
