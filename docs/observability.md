# Observability: running Ohmline's telemetry locally

Every Ohmline service exports traces, metrics and logs over OTLP when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set (`Ohmline.ServiceDefaults`, section 17.2). Under Aspire the
dashboard receives them. This page runs the production-shaped stack instead (section 18.7):

```text
node, gateway, billing ──OTLP──▶ Collector 0.142.0 ──▶ Prometheus 3 (metrics, OTLP receiver)
                                                   └─▶ Tempo (traces), Loki (logs)
                               Grafana ◀── all three
```

`deploy/observability/compose.yaml` starts two containers: the Collector, and
`grafana/otel-lgtm` 0.39.0, which runs Prometheus (started with `--web.enable-otlp-receiver`),
Tempo, Loki and Grafana in one container. One container for four backends is a laptop's
convenience; in production each runs on its own, and the Collector's configuration is what
changes, not the services'.

## Start the stack

With Podman (rootless works) or Docker:

```bash
podman compose -f deploy/observability/compose.yaml up -d
```

| What | Where |
|---|---|
| OTLP for the services (gRPC / HTTP) | `localhost:4317` / `localhost:4318` |
| Grafana (user `admin`, password `admin`) | <http://localhost:3000>, dashboard *Ohmline: Charging* in folder *Ohmline* |
| Prometheus (UI and query API) | <http://localhost:9090> |

Stop it with `podman compose -f deploy/observability/compose.yaml down`.

## Point a node at it

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_SERVICE_NAME=node
export OTEL_METRIC_EXPORT_INTERVAL=5000      # milliseconds; the default is 60000
dotnet run --project src/Ohmline.Node        # Development: SQLite, a cluster of one, on :5180
```

The launch profile sets the address (`http://localhost:5180`); `ASPNETCORE_URLS` does not
override it unless you pass `--no-launch-profile`. Under Aspire, leave all of this alone: the
AppHost sets the variables for every project and the Aspire dashboard is the receiver.

## The Collector's job

`deploy/observability/collector-config.yaml` receives OTLP and sends:

- **metrics** with the `otlphttp` exporter to `http://lgtm:9090/api/v1/otlp`, Prometheus 3's
  own OTLP receiver. No service exposes a `/metrics` scrape endpoint, and the beta
  `OpenTelemetry.Exporter.Prometheus.AspNetCore` package is not used;
- **traces** and **logs** to the LGTM container's collector, which hands them to Tempo and Loki.

On the way it limits its memory (`memory_limiter`), batches, and drops spans of health probes
(`filter/probes`). Sampling, scrubbing attributes and sending to a second backend are changes
to this file alone.

## From OpenTelemetry names to PromQL

Prometheus 3 translates each OTLP metric name: dots become underscores, the unit becomes a
suffix (curly-brace annotations such as `{command}` are dropped), and a counter gets `_total`.
Attributes become labels the same way, and resource attributes such as `service.name` are
promoted to labels (`service_name`, and `job`). Observed with the node at `ch18-s07`:

| Instrument (OpenTelemetry) | Unit | Series in Prometheus |
|---|---|---|
| `ohmline.charging.commands` (counter) | `{command}` | `ohmline_charging_commands_total` |
| `ohmline.charging.commands.rejected` (counter) | `{command}` | `ohmline_charging_commands_rejected_total` |
| `ohmline.charging.sessions.started` (counter) | `{session}` | `ohmline_charging_sessions_started_total` |
| `ohmline.charging.energy.delivered` (histogram) | `kWh` | `ohmline_charging_energy_delivered_kWh_bucket`, `_sum`, `_count` |
| `ohmline.site_energy.allocation.utilization` (gauge) | `1` | `ohmline_site_energy_allocation_utilization_ratio` |
| `ohmline.akka.recovery.duration` (histogram) | `s` | `ohmline_akka_recovery_duration_seconds_bucket`, `_sum`, `_count` |
| `ohmline.akka.sharding.entities` (gauge) | `{entity}` | `ohmline_akka_sharding_entities` |
| `ohmline.akka.dead_letters` (counter) | `{message}` | `ohmline_akka_dead_letters_total` |
| `ohmline.charging.journey.time_to_energy_flow` (histogram) | `s` | `ohmline_charging_journey_time_to_energy_flow_seconds_bucket`, `_sum`, `_count` |

Tags become labels: `command.type` is `command_type`, `aggregate.type` is `aggregate_type`.
A refusal at `ch18-s07`, as Prometheus stores it:

```text
ohmline_charging_commands_rejected_total{aggregate_type="charge-point",
  command_type="ReserveConnector", reason="UnknownConnector", job="node", service_name="node",
  deployment_environment_name="Development"} 3
```

Queries the dashboard uses:

```text
sum by (reason) (rate(ohmline_charging_commands_rejected_total[5m]))
histogram_quantile(0.95, sum by (le) (rate(ohmline_charging_energy_delivered_kWh_bucket[1h])))
max(ohmline_site_energy_allocation_utilization_ratio)
max by (sharding_type) (ohmline_akka_sharding_entities)
```

Two of them take `max` across nodes on purpose: each node reports its fullest site, and each
node's shard poller reports the whole cluster's figures (section 18.2).

## The dashboard

`deploy/observability/dashboards/ohmline-charging.json` is provisioned into Grafana by
`grafana-dashboards.yaml`, read from the mounted folder; edit the JSON and Grafana picks the
change up within a few seconds. Panels: sessions started, energy delivered and energy per
session; commands and refusals by reason; the fullest site; and the actor shell's recovery
times, dead letters, entities and actor lifecycles.

## The service level objective

`deploy/observability/alerts/ohmline-slo.yaml` holds Ohmline's first SLO (section 18.9): a driver
who says "start charging" gets energy flowing within 60 seconds, for 99% of journeys over 28
days. The SLI is measured by the journey from its own events: from `JourneyStarted` to
`EnergyFlowStarted`, or to `JourneyAbandoned` when it gives up first, with the outcome as a tag
(`energy-flowing`, or the abandon reason). Journeys ended by a refused token or a declined hold
are the driver's, not Ohmline's, and are left out.

The file has four recording rules, the error ratio over 5 minutes, 30 minutes, 1 hour and 6
hours, and two alerts that burn the 1% budget at two speeds:

| Alert | Fires when | Severity |
|---|---|---|
| `TimeToEnergyFlowBudgetBurningFast` | 1 h and 5 min error ratios above 14.4 × 1% | `page` |
| `TimeToEnergyFlowBudgetBurningSlowly` | 6 h and 30 min error ratios above 6 × 1% | `ticket` |

compose.yaml mounts the rules and `prometheus.yaml` (the image's own configuration plus
`rule_files`), so Prometheus evaluates them: see <http://localhost:9090/alerts>. Routing alerts
to people (Alertmanager, or Grafana's alerting) is a production concern this stack leaves out.
Check the rules after an edit with the `promtool` inside the image:

```bash
podman run --rm --entrypoint /otel-lgtm/prometheus/promtool \
  -v ./deploy/observability/alerts:/a:ro,Z docker.io/grafana/otel-lgtm:0.39.0 \
  check rules /a/ohmline-slo.yaml
```
