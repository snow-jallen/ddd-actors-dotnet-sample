# Ohmline on Kubernetes

*Section 21.6. What happens during a rolling update is section 21.7.*

| Path | What it is |
|---|---|
| `crds/akka-leases.yaml` | The `leases.akka.io` CRD the split-brain resolver's lease needs. Once per Kubernetes cluster, by its administrator. |
| `base/` | Everything an environment shares: namespace, service account and role, settings, the migrator job, the node, gateway and Billing deployments and services, the headless service of the Akka.NET cluster, disruption budgets. |
| `overlays/dev/` | One pod of each, namespace `ohmline-dev`. |
| `overlays/prod/` | Three nodes and two gateways, namespace `ohmline`, the released image tags. |

## How the cluster forms

The node and gateway pods carry the label `akka-cluster: ohmline` and run with
`OHMLINE_KUBERNETES=true` (`base/configmap.yaml`). Each node then:

1. binds Akka.Remote on `0.0.0.0:4053` and advertises its pod IP (`status.podIP`);
2. starts Akka.Management's HTTP endpoint on port 8558;
3. asks the Kubernetes API for the pods labelled `akka-cluster=ohmline`, which its service
   account may list (`base/rbac.yaml`);
4. probes each of them on port 8558: if one is already a member, it joins that cluster; if none
   is, and it has found at least `Ohmline__Kubernetes__RequiredContactPoints` of them, the pod with
   the lowest address forms a new cluster and the others join it.

A pod is ready only once it is an Up member and reaches its database (`/health/ready`). The
headless service `ohmline-akka` publishes not-ready addresses, so pods can find each other before
any of them is ready.

When the cluster splits, the side that takes the lease `ohmline-akka-sbr` (lease-majority) stays
up and the other downs itself; Kubernetes restarts its pods, which join again.

## Before the first apply

```bash
kubectl apply -f deploy/k8s/crds/akka-leases.yaml       # once per Kubernetes cluster
kubectl create namespace ohmline
kubectl -n ohmline create secret generic ohmline-db \
  --from-literal=ohmline='Host=...;Database=ohmline;Username=ohmline_app;Password=...' \
  --from-literal=billing='Host=...;Database=billing;Username=billing_app;Password=...'
kubectl -n ohmline create secret generic ohmline-db-owner \
  --from-literal=ohmline='Host=...;Database=ohmline;Username=ohmline_owner;Password=...' \
  --from-literal=billing='Host=...;Database=billing;Username=billing_owner;Password=...'
```

The applications' roles are the least-privilege ones the migrator grants (section 20.8); only the
migrator's secret may change the schema.

## Apply

```bash
kubectl kustomize deploy/k8s/overlays/prod               # what would be applied
kubectl apply -k deploy/k8s/overlays/prod
kubectl -n ohmline rollout status deployment/node
```

Both overlays render with `kubectl kustomize` (kubectl v1.34.1) and validate with
`kubeconform -strict` against Kubernetes 1.34. They have not been applied to a running cluster
in this repository's checks.
