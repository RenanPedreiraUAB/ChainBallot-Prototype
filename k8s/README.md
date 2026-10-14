# Kubernetes Autoscaling Demo (Inbound-Broker-Outbound)

This folder provides a repeatable scalability scenario for the inbound-broker-outbound architecture.

The demo is intentionally focused:
- inbound API + queue consumer: `outbound` (`/api/processing/ingest` + Kafka consumer loop)
- broker: Redpanda
- autoscaling:
  - `HPA` for CPU-based scaling
  - optional `KEDA` for Kafka lag-based scaling

## 1) Prerequisites

- Kubernetes cluster (kind, minikube, or Docker Desktop Kubernetes)
- `kubectl`
- metrics-server installed (required for HPA metrics)

Optional:
- KEDA installed for queue-lag autoscaling

## 1.1) Baseline vs Stress-Test State

Use this section to keep the prototype in a clear "optimal baseline" state and make temporary stress changes explicit.

Baseline (optimal prototype state for normal demos):
- Apply only `k8s/chainballot-autoscale-demo.yaml`.
- Keep topic settings at baseline values (no partition inflation).
- Run traffic spikes through `k8s/spike-ingest.ps1`.
- Use CPU HPA results as observed behavior, even if no scale-up occurs.

Stress-test additions (temporary, for proving elasticity limits):
- Install KEDA and apply `k8s/keda-outbound-kafka-scaledobject.yaml`.
- Explicitly create/adjust Kafka topic and backlog conditions to force lag signals.
- Any temporary partition increase or backlog injection must be logged as test-only.

Reset back to baseline after stress tests:

```powershell
kubectl delete namespace chainballot
kubectl apply -f .\k8s\chainballot-autoscale-demo.yaml
```

## 2) Load Local Image Into Cluster

The outbound deployment references `chainballot-local-outbound:latest`.
Build it from repository root first:

```powershell
docker compose -f docker-compose.local.yml build outbound
```

If using kind, load the image:

```powershell
kind load docker-image chainballot-local-outbound:latest
```

For Docker Desktop Kubernetes, image is typically already available.

## 3) Apply Baseline Autoscaling Stack (HPA)

```powershell
kubectl apply -f .\k8s\chainballot-autoscale-demo.yaml
kubectl -n chainballot get pods
kubectl -n chainballot get hpa outbound-hpa -w
```

Expose outbound locally:

```powershell
kubectl -n chainballot port-forward svc/outbound 5301:5301
```

## 4) Run Spike to Trigger Scale-Up

In a second terminal:

```powershell
.\k8s\spike-ingest.ps1 -TotalRequests 8000 -ParallelJobs 36 -ElectionId election-scale-hpa
```

Observe scale behavior:

```powershell
kubectl -n chainballot get hpa outbound-hpa -w
kubectl -n chainballot get pods -l app=outbound -w
```

## 5) Optional Queue-Lag Autoscaling (KEDA)

Install KEDA (once per cluster):

```powershell
kubectl apply --server-side -f https://github.com/kedacore/keda/releases/download/v2.15.1/keda-2.15.1.yaml
```

Apply scaled object:

```powershell
kubectl apply -f .\k8s\keda-outbound-kafka-scaledobject.yaml
kubectl -n chainballot get scaledobject
kubectl -n chainballot get hpa -w
```

KEDA trigger details:
- topic: `chainballot-ballots`
- consumer group: `chainballot-outbound`
- scale condition: lag above threshold

Important:
- KEDA lag tests are valid scalability experiments, but they are controlled stress scenarios.
- Keep baseline and stress-test evidence separate in validation reporting.

## 6) Evidence to Capture

Use these artifacts as scalability evidence in project reporting:
- `kubectl get hpa -n chainballot` before/during/after spike
- `kubectl get pods -n chainballot -l app=outbound` replica timeline
- spike script parameters and request totals
- note on architecture rationale: broker decouples inbound burst from processing pace
- optional KEDA lag metrics and scale events
- explicit list of temporary stress actions applied (for example topic creation, backlog injection, partition changes)

## 7) Cleanup

```powershell
kubectl delete namespace chainballot
```
