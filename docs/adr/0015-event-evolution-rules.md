# 0015. Events evolve by expanding, never by changing in place

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch20-s02

## Context

ADR 0007 made serializer ids and manifests permanent. That says what must never change; it does
not yet say how a stored shape is allowed to grow. Ohmline now has a reason to grow one: Site
Energy wants to know how many phases a car charges on, and the natural place to record it is
`SessionStarted`, of which thousands are already in the journal without it.

Three kinds of reader meet every shape, and each runs code of a different age:

- **Recovery and projections** read every event ever written, with today's code.
- **A node in a rolling update** reads messages and events written by the next release, with
  yesterday's code, for as long as the update takes.
- **Consumers of the Published Language and of the client contracts** read with code we do not
  deploy at all.

System.Text.Json, as `ManifestJsonSerializer` configures it, skips members it does not know
(`UnmappedMemberHandling.Skip`) and, with `RespectRequiredConstructorParameters`, refuses JSON
that lacks a constructor parameter, unless that parameter has a default. A nullable parameter
without a default is still required: `int? PhaseCount` alone would make every stored
`SessionStarted` unreadable.

## Decision

We will evolve every stored or sent shape by these rules:

1. **Only add, and only optional fields.** A new field is a constructor parameter with a default
   (`int? PhaseCount = null`) or an `init` property with one, at the end of the record. The
   manifest stays. Code that reads it gives the missing value a meaning in one place: for an
   event, in `Evolve` (`started.PhaseCount ?? SessionTerms.AssumedPhaseCount`), never in
   `Decide`.
2. **Read before you write.** A release that makes a node *write* a new field, a new manifest
   version or a new type comes after a release in which every node can *read* it. For
   `PhaseCount`: section 20.2's release reads it and records it when a command carries one; only
   a later release of the Device Gateway sends it. Rolling back the second release is then safe,
   and rolling back the first is safe as long as the second has not started.
3. **Never change a field's meaning, unit or type, and never remove or rename it in place.** That
   is a new manifest version, upcast on read (sections 20.3 and 20.4). To stop using a field:
   stop writing it, keep reading it.
4. **Expand, migrate, contract.** When a change needs a new shape, first expand (read both, write
   the old), then switch the writers to the new one, and only then contract: stop writing the
   old. For stored events the contract step never deletes the reader of the old shape.
5. **Freeze before you accept.** A golden file that changes because a field was added is copied,
   unchanged, to `tests/golden/corpus/` before the new one is accepted, and a test reads the
   frozen copy with today's code.

## Consequences

- Adding a field costs a default, a line in `Evolve` or in the snapshot mapping, and a frozen
  golden file. In exchange, no stored event ever needs rewriting for it.
- Every new field is nullable or defaulted in the type, even when the domain always knows it from
  now on. The assumed value for older data (`SessionTerms.AssumedPhaseCount`) is a business
  decision, written down in one constant.
- A shape grows by one release at a time; a field that a writer and its readers need at once
  takes two releases.
- `ManifestJsonSerializerTests` shows both outcomes on test messages (a defaulted field read from
  older JSON, a nullable one without a default refused). `OlderShapeTests` reads the frozen
  `SessionStarted`, `StartSession` and session snapshot JSON. `ChargingSessionTests` checks the
  phase count is locked at the start and assumed for an older start.
