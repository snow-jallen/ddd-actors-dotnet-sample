# House style

The rules the sample's C# follows, and why. Each rule points at the learning test that pins the
behaviour down, in `learn/Ch03.LanguageLab.Tests` unless another project is named. When a new
SDK changes a behaviour, that test fails first, and this page changes with it.

## Unions or closed records

[ADR 0003](adr/0003-unions-vs-closed-records.md) records the decision. In short:

| Kind | Construct |
|---|---|
| Told, asked, replied, persisted, or on an HTTP or feed wire: commands, replies, queries, domain events, snapshot DTOs, integration events | `closed record` base, `sealed record` leaves, every intermediate level `closed` |
| In process only: aggregate state, decisions, error sets, `Result<T, E>`, effects, parse and translation results, actor-side protocol unions, multi-source inputs | `union` over existing case types, usually `sealed record`s |

- A union and its case records live in one file named after the union. A closed hierarchy
  lives in one file named after its base type. Otherwise, one type per file.
- A plain `abstract record` is not used for a closed set: outside its assembly every switch
  needs a discard arm (`ClosedHierarchyTests`).

## Switches and exhaustiveness

- Decision tables are switch **expressions**. Only switch expressions are checked for missing
  cases; switch statements are not.
- CS8509 (a missing case) and CS8655 (a missing `null`) are build errors, set in
  `Directory.Build.props`, even when `TreatWarningsAsErrors` is off.
- No discard arm over a union or a closed hierarchy. A `_ =>` arm hides the next case someone
  adds.
- Every switch that a `default` union can reach has a `null =>` arm. A default union holds no
  case, and without that arm the switch throws `SwitchExpressionException`; the compiler does
  not warn (`UnionTests`). Actor state fields, array slots and anything deserialized can be
  default.
- A union nested in another union needs one explicit hop: `(ConnectorStatus)new Occupied(...)`.
  Conversions do not chain, and patterns do not reach into a nested union; write
  `report is ConnectorStatus and Occupied` (`UnionTests`).
- Log a union's `.Value`, not the union: a union's `ToString()` prints only its type name.

## Values, results and errors

- Value objects are `readonly record struct`s whose `init` accessor validates with the `field`
  keyword, so the constructor and every with-expression pass the guard (`FieldKeywordTests`).
  Where a `default` value would be invalid and must never exist, use a `sealed record` with a
  private constructor instead.
- Every value object implements `IValueObject<TSelf, TValue>` (`Ohmline.SharedKernel`).
  `TryCreate` returns `Result<TSelf, ValidationError>`; `Create` is for values known to be valid.
- Expected failures are values, not exceptions: return `Result<T, E>` and chain with `Map`,
  `Bind` and `Match` (`tests/Ohmline.SharedKernel.Tests/ResultTests`). Exceptions are for bugs.
- Quantities implement the generic-math operator interfaces they support, so algorithms such as
  sums and clamps are written once (`GenericMathTests`).

## Time and identity

- Code that needs the time takes a `TimeProvider`; tests pass a `FakeTimeProvider`
  (`TimeTests`). Nothing reads `DateTime.UtcNow`. Deciders take the time as data, in a
  `DecisionContext` (from section 7.4).
- New identifiers are Guid version 7 from `GuidV7.New(clock)`: they carry the clock's time and
  sort in creation order (`tests/Ohmline.SharedKernel.Tests/Identity/GuidV7Tests`).

## Serialization

These rules come from `SerializationRealityTests` (System.Text.Json) and `AkkaUnionTests`
(Akka.NET 1.5.73).

- **Never `Tell`, `Ask`, reply with or `Persist` a union.** `Tell` takes `object`, so the union
  arrives boxed as the union struct: `Receive<Occupied>` does not see it, only
  `Receive<ConnectorStatus>` does. Send the case, `.Value`, through a typed `Send` (section 10.2).
- **Akka.NET 1.5's default serializer loses a union's case.** Newtonsoft.Json writes the case
  and reads the union back with `Value == null`, without an exception. Every actor test runs
  with `akka.actor.serialize-messages = on` (from section 10.4), so this shows up in tests, as an
  empty union, and not first in production.
- **System.Text.Json writes a union as its bare case,** with no discriminator. A union whose
  cases are all JSON objects cannot be read back (`JsonException`: "ambiguous"), and the source
  generator reports SYSLIB1227. `JsonUnionTypeStructuralClassifier` helps only when every case
  has a property no other case has; otherwise it refuses the union with `NotSupportedException`.
  Unions are not a wire format.
- **A closed record needs polymorphism switched on, or leaf fields vanish without an error.**
  Use `[JsonPolymorphic(InferClosedTypePolymorphism = true)]`, the same option on
  `JsonSerializerOptions`, or explicit `[JsonDerivedType]` discriminators.
- **Names on the wire and in the journal are chosen, never derived from type names.** An
  inferred discriminator is the type name, and a rename breaks everything already stored. From
  section 10.3, each context's serializer maps types to explicit, versioned manifests such as
  `charging.connector-reserved.v1`.

## Layout

- Classes are `sealed` unless designed for inheritance. Namespaces are file-scoped and follow
  the project name and folders.
- Lines in `.cs` files are at most 100 characters, so the book can quote them unchanged.
- No `#region`s, and no comments that restate the code.
- Code that must not compile, kept to show a compiler error, sits in an
  `#if DEMO_COMPILE_ERRORS` block.
