# 0003. Closed records on the wire, unions in the core

- **Status:** Accepted
- **Date:** 2026-11-04
- **Book section:** ch03-s04

## Context

C# 15 gives the sample two ways to say "exactly one of these cases", and the compiler checks
every switch expression over either of them for missing cases:

- A **union** (`public union ConnectorStatus(Available, Occupied, Faulted);`) is a struct that
  holds one value of a fixed list of existing types in an `object? Value`. The cases need not be
  related, so a union can combine types from different modules, primitives, or another union.
  It has no fields of its own, and `default(ConnectorStatus)` holds no case at all.
- A **closed hierarchy** (`public closed record ChargerCommand(string ChargePointId);` with
  `sealed record` leaves) is an ordinary abstract base class that only its own assembly can
  derive from. Every case *is a* `ChargerCommand`, shares its members, and is a single
  reference.

The two differ most where a value leaves the function that made it. The learning tests in
`learn/Ch03.LanguageLab.Tests` pin these differences down (sections 3.3, 3.4 and 3.9):

- **Akka.NET's `Tell` takes `object`.** A union is boxed as the union struct, so
  `Receive<Occupied>` does not see it; only `Receive<ConnectorStatus>` does. A closed record is
  received by both its leaf handler and its base handler.
- **Akka.NET 1.5's default serializer (Newtonsoft.Json) loses the case.** A union
  round-trips with `Value == null`, without an exception. Closed records round-trip.
- **System.Text.Json writes a union as the bare case,** with no discriminator, and cannot read
  object-shaped cases back without a type classifier. A closed record gets a stable
  discriminator from `[JsonDerivedType]` or `InferClosedTypePolymorphism`.
- **A plain `abstract record` is not exhaustive** outside its assembly: every switch needs a
  discard arm, which also hides the next forgotten case.

## Decision

We will use a `closed record` base with `sealed record` leaves for anything that is sent between
actors, persisted or put on a wire: commands, replies, queries, domain events, snapshot DTOs and
integration events. Every intermediate level of such a hierarchy is `closed` as well.

We will use a `union` for values that stay in process: aggregate state, decisions, error sets,
`Result<T, E>`, effects, parse and translation results, actor-side protocol unions, and the
inputs of a process manager that listens to several contexts.

| Kind | Construct |
|---|---|
| Told, asked, replied, persisted, or on an HTTP or feed wire | `closed record` base, `sealed record` leaves |
| State, decisions, errors, results, effects, multi-source inputs | `union` over `sealed record` cases |

We will never `Tell`, `Ask`, reply with, or `Persist` a union value. Code that holds a union
sends its `.Value`, which is a case record. Every switch that a `default` union can reach has a
`null =>` arm rather than a `_ =>` arm, so that a new case still breaks the build. A union and
its case records share one file named after the union; a closed hierarchy lives in one file
named after its base type.

## Consequences

- Adding a command, an event or a union case breaks the build at every switch expression that
  does not handle it. `Directory.Build.props` makes CS8509 and CS8655 errors even when
  `TreatWarningsAsErrors` is turned off.
- Messages work with Akka.NET's `Receive<TBase>` and with any serializer that understands class
  hierarchies, without casts or custom converters.
- The domain core keeps the precise types: a decision can be one of several unrelated records,
  and an error set can combine errors from different modules.
- Two constructs means one more thing to learn and one more question in review. The table above
  is the answer, and `docs/house-style.md` (section 3.9) repeats it with the serialization rules.
- The shell translates between the two at the boundary: it sends and persists case records and
  lifts received records back into the core's unions.
- A union holding a value type boxes it. At the sizes in this sample that does not matter.
- Only switch expressions are checked. Switch statements over these types are not, so the
  sample uses switch expressions for decision tables.
- Revisit this decision when Akka.NET 1.6 ships: its `Akka.Serialization.V2` (1.6.0-beta5)
  round-trips union fields, which may allow unions in some messages (Chapter 21).

## References

- "Union types (C# reference)", Microsoft Learn,
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union
- "Closed hierarchies" feature specification, Microsoft Learn,
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-15.0/closed-hierarchies
- "Serialize union types with System.Text.Json", Microsoft Learn,
  https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/union-types
