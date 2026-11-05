# 0013. Spans keep their static sources; .NET 11's AddTracing waits

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch18-s06

## Context

.NET 11 adds to tracing what .NET 8 gave metrics: `services.AddTracing()` registers an
`ActivitySourceFactory`, which makes `ActivitySource`s scoped to a container, the way
`IMeterFactory` makes meters (section 18.1), and `ITracingBuilder` adds rules, much like logging
filters, that enable or disable sources and operations per listener, in code or from
configuration. `ActivitySource` is no longer sealed: the factory hands out a subclass.

Two traps when reading the documentation:

- `AddTracing` is in `Microsoft.Extensions.DependencyInjection`, but the rule methods
  (`EnableTracing`, `DisableTracing`, `AddListener`) are extension methods in
  `Microsoft.Extensions.Diagnostics.Tracing`. Without
  `using Microsoft.Extensions.Diagnostics.Tracing;` the compiler reports CS1061,
  `'ITracingBuilder' does not contain a definition for 'DisableTracing'`, and the
  documentation's samples leave the `using` out.
- Neither `Host.CreateApplicationBuilder()` nor `WebApplication.CreateBuilder()` registers the
  factory: without `AddTracing()` there is no `ActivitySourceFactory` to resolve.

What we verified, on SDK 11.0.100-rc.3.26522.148 with OpenTelemetry 1.20.1 (`AddTracingTests`):

1. The OpenTelemetry SDK exports the spans of a factory-made source: `AddSource("Ohmline.*")`
   listens by name, and the factory's sources are ordinary sources to it.
2. The factory gives the same source for the same name, scoped to itself (`Scope` is the
   factory), just as `IMeterFactory` caches meters.
3. **The rules do not reach the SDK.** `DisableTracing("Ohmline.…")` applies to the listeners
   added through `ITracingBuilder.AddListener`; the SDK registers its own `ActivityListener`
   and goes on exporting the "disabled" source's spans.
4. **A factory's source dies with its container.** Once the service provider is disposed, the
   source starts no activity, whoever listens.

Ohmline's spans come from static sources, one per context (`ChargingDiagnostics.ActivitySource`,
section 17.1), started from places the container does not make: the actor shell's base classes,
the projection runner inside an Akka.NET stream, the gateway's socket loop.

## Decision

We will keep one static `ActivitySource` per context, enabled by the OpenTelemetry SDK's
`AddSource`, and will not call `AddTracing()` in any host. `ChargingDiagnostics` is not switched
to the factory.

We would adopt `AddTracing` when all of these hold:

- .NET 11 is released (the API is in a release candidate as we write);
- the OpenTelemetry SDK honours `TracingOptions`' rules, or offers its listener as an
  `ITracingBuilder` listener, so that one set of rules governs what is exported;
- a test isolation problem appears that trace ids cannot solve: today every telemetry test reads
  only the spans of its own trace (section 17.3), so per-container sources buy nothing yet.

## Consequences

- Metrics and traces are made differently: meters through `IMeterFactory` (they need it, to keep
  tests' measurements apart and to be disposed with the host), sources statically. The book says
  why, rather than letting it look like an oversight.
- Turning a noisy source off is done in the SDK's own configuration (leave it out of
  `AddSource`, or filter in the Collector, section 18.7), not with `DisableTracing`.
- If we adopt the factory later, every span site must receive its source from the container:
  the shell's actors through their constructors, the projection runner through the daemon, and
  a source must never be used after its host is disposed, which a static one never risks.
