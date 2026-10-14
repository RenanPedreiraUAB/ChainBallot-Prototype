# Test Plan and Validation

## 1. Purpose

This document defines the validation scope for the Chain Ballot prototype as exercised inside the local Docker environment.

The goal is to prove that the implementation is coherent, reproducible, and sufficiently realistic as prototype validation evidence, without requiring a live tester group or production deployment.

## 2. Validation Scope

The validation scope is intentionally limited to the Docker-based prototype and the repository artifacts that support it.

In scope:
- local Docker stack startup and service health
- repository structure and buildability
- component test suites
- end-to-end ballot submission and anchoring
- election close and final tally publication
- result portal display
- VID verification
- watcher audit

Out of scope:
- production orchestration
- HA and disaster recovery
- cloud secret management
- external tester recruitment
- live-host certification

## 3. Test Environment

The following environment is used for validation:
- Windows workstation
- Docker Desktop with the local stack defined in `docker-compose.local.yml`
- .NET 9 SDK
- PowerShell scripts for seeding and smoke testing

The local stack includes:
- CynetAuth
- Election Authorization
- Voter Component
- Election Processing
- Transparency API
- Result Portal
- Watcher API
- Threshold Decryption API
- Redpanda
- Metadata service (writable local publish/read API)
- Solana local validator

## 4. Test Strategy

Validation is performed in layers:

1. Component-level tests verify each service in isolation.
2. Local stack checks verify the Docker environment starts cleanly and the APIs respond.
3. End-to-end smoke tests verify the full election flow from registration to final result publication.
4. Result and audit checks verify that the user-facing outputs match the finalized state.

This validation report uses all existing prototype test assets, not only the Docker smoke flow.
That means the validation evidence includes unit and integration tests for each component solution, repo-wide scripted test execution, and one or more Docker E2E runs that exercise the full election lifecycle.

## 5. Test Scenarios

### 5.0 Existing Prototype Test Suites

| ID | Suite | What it covers | How it is run | Expected result | Observed result |
| --- | --- | --- | --- | --- | --- |
| TS-00 | ConfigurationComponent.Tests | Validation, deployment, metadata, and manifest behavior | `dotnet test .\Configuration\ConfigurationComponent.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-01 | CynetAuth.Api.Tests | Register/login/JWT issuance and local auth behavior | `dotnet test .\CynetAuth\CynetAuth.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-02 | ElectionAuthorization.Api.Tests | VID/eligibility/grant/consume behavior | `dotnet test .\Authorization\ElectionAuthorization.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-03 | VoterComponent.Web.Tests | Ballot UI, auth state, and submission flow | `dotnet test .\Voter\VoterComponent\VoterComponent.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-04 | ElectionProcessing.Api.Tests | Queueing, batching, anchoring, proof strategy, and retry behavior | `dotnet test .\ElectionProcessing\ElectionProcessing\ElectionProcessing.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-05 | TransparencyAudit.Api.Tests | Snapshot, verification, canonical-state, and reconciliation behavior | `dotnet test .\Transparency\TransparencyAudit.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-06 | ResultPortal.Web.Tests | Verified results and VID verification UI | `dotnet test .\Result\ResultPortal.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-07 | WatcherAudit.Api.Tests | Audit consistency, report generation, and operator access | `dotnet test .\Watcher\WatcherAudit.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |
| TS-08 | ThresholdDecryption.Api.Tests | Ceremony, approval, share, finalize, and publish-final-tally behavior | `dotnet test .\Decryption\ThresholdDecryption.sln --no-restore` or `scripts/test-all.ps1` | All tests pass | Passed |

These suites establish the base correctness of the prototype before the Docker E2E validation is used to prove the integrated flow.

### 5.1 Repository and Build Validation

| ID | Scenario | How it is tested | Expected result | Observed result |
| --- | --- | --- | --- | --- |
| BV-01 | Component solutions restore and test cleanly | Run `scripts/test-all.ps1` | All component test suites pass | Passed |
| BV-02 | Local stack can be built from repository root | Run `docker compose -f docker-compose.local.yml up --build -d` | All images build and containers start | Passed in local smoke runs |

### 5.2 Local Docker Runtime Validation

| ID | Scenario | How it is tested | Expected result | Observed result |
| --- | --- | --- | --- | --- |
| DR-01 | Core services are healthy | Query health endpoints for CynetAuth, Authorization, Outbound, Transparency, and Watcher | Each endpoint returns HTTP 200 | Passed |
| DR-02 | Docker storage and runtime remain usable after cleanup | Remove local Docker VHDX and rerun stack build | Docker images rebuild and services restart | Passed |
| DR-03 | Seeded demo users are created consistently | Run `docker/seeding/seed-users.ps1` with eligibility seeding enabled | 200 users are registered or already exist, and eligibility is seeded | Passed |
| DR-04 | Docker data location remains on D: after reboot | Verify AppData junction targets and check `D:\DockerDesktopData\LocalDocker\wsl\disk\docker_data.vhdx` plus `ext4.vhdx` | Heavy Docker writable data remains on D: and Docker engine remains healthy | Passed |

### 5.3 End-to-End Election Validation

| ID | Scenario | How it is tested | Expected result | Observed result |
| --- | --- | --- | --- | --- |
| E2E-01 | Full election submission cycle | Run `docker/e2e-smoke.ps1` with the Docker stack running | 200 ballots are submitted and processed | Passed |
| E2E-02 | Election close and final tally publication | The smoke flow proposes/finalizes threshold decryption, publishes the final tally, and ingests the finalized transparency snapshot | Snapshot lifecycle becomes `Finalized` and candidate tallies are present | Passed |
| E2E-03 | Result portal shows the finalized election | Open `http://localhost:5181/Results?ElectionId=<election-id>` after the smoke completes | Portal displays the finalized election snapshot and candidate tallies | Passed |
| E2E-04 | VID verification works after finalization | Open `http://localhost:5181/Verify?ElectionId=<election-id>&Vid=<vid>` | Portal returns a verified result for a valid VID | Passed |
| E2E-05 | Watcher audit runs against finalized state | Run the watcher audit step from the smoke flow | Audit report is generated and stored | Passed |

The Docker E2E evidence is intentionally broader than a single happy-path submission.
The smoke flow now acts as a mini validation suite that covers registration, authorization, queue submission, anchoring, finalization, result publication, and audit evidence.

### 5.4 Negative and Resilience Checks

| ID | Scenario | How it is tested | Expected result | Observed result |
| --- | --- | --- | --- | --- |
| NEG-01 | Transparency snapshot before finalization | Query the result portal or Transparency API before the election is finalized | No finalized result is shown | Passed |
| NEG-02 | Docker data reset | Remove the Docker VHDX and restart the environment | The environment rebuilds from source and seed scripts repopulate the data | Passed |
| NEG-03 | Outbound anchor failure recovery | Rebuild outbound and rerun the smoke after fixing the Solana RPC client issue | The 200-voter flow completes after the fix | Passed |

### 5.5 Scalability Demonstration (Kubernetes)

| ID | Scenario | How it is tested | Expected result | Observed result |
| --- | --- | --- | --- | --- |
| SC-01 | CPU-driven autoscaling for inbound burst | Deploy `k8s/chainballot-autoscale-demo.yaml`, run `k8s/spike-ingest.ps1` with high request count, monitor `kubectl get hpa` and pod replicas | Outbound replicas increase under load and scale back after stabilization | Ready to run in K8s demo environment |
| SC-02 | Broker-lag driven autoscaling | Install KEDA and apply `k8s/keda-outbound-kafka-scaledobject.yaml`, then run spike load | Replicas scale according to Kafka lag threshold, showing queue-decoupled elasticity | Ready to run in K8s demo environment |

This scalability section is intentionally framed as a demonstration scenario rather than a production benchmark.
It provides evidence that the inbound-broker-outbound architecture can be coupled with autoscaling controls that react to bursty workload conditions.

### 5.6 Baseline State vs Test-Only Modifications

To keep prototype reporting precise, validation distinguishes baseline runtime state from temporary stress instrumentation.

Baseline (optimal prototype state):
- Docker: normal full-cycle flow with configured voter volume, no ad-hoc runtime mutations.
- Kubernetes: `k8s/chainballot-autoscale-demo.yaml` with standard CPU HPA settings.
- No temporary Kafka partition inflation or synthetic backlog injection applied.

Test-only modifications (must be explicitly disclosed):
- Enabling KEDA lag autoscaling (`k8s/keda-outbound-kafka-scaledobject.yaml`).
- Creating/altering Kafka topic state specifically to force lag-driven activation.
- Injecting synthetic backlog and temporary partition changes to cross scaler thresholds.

Rule for reporting:
- Baseline findings and stress findings are both valid, but they must be reported separately.

## 6. Performed Results

The following results were observed in the current repository state, accumulated across validation sessions from 2026-07-16 through 2026-10-14:

### 6.1 Repository Test Suites

- `scripts/test-all.ps1` completed successfully across all component solutions.
- Total executed tests across suites: 157.
- Per-suite summary from the run (updated 2026-10-14 after the auth-header/digest-attachment fixes to the Processing → Transparency evidence pipeline):
	- ConfigurationComponent.Tests: 27 passed
	- CynetAuth.Api.Tests: 4 passed
	- ElectionAuthorization.Api.Tests: 9 passed
	- VoterComponent.Web.Tests: 21 passed
	- ElectionProcessing.Api.Tests: 38 passed
	- TransparencyAudit.Api.Tests: 28 passed
	- ResultPortal.Web.Tests: 10 passed
	- WatcherAudit.Api.Tests: 6 passed
	- ThresholdDecryption.Api.Tests: 14 passed

### 6.2 Docker Storage and Runtime Verification

- Docker AppData junctions remained active after reboot and pointed to D: paths:
	- `C:\Users\renan\AppData\Local\Docker -> D:\DockerDesktopData\LocalDocker`
	- `C:\Users\renan\AppData\Roaming\Docker -> D:\DockerDesktopData\RoamingDocker`
	- `C:\Users\renan\AppData\Roaming\Docker Desktop -> D:\DockerDesktopData\RoamingDockerDesktop`
- Heavy Docker writable files were confirmed on D:
	- `D:\DockerDesktopData\LocalDocker\wsl\disk\docker_data.vhdx` (4.32 GB)
	- `D:\DockerDesktopData\LocalDocker\wsl\main\ext4.vhdx` (0.09 GB)
- `C:\ProgramData\DockerDesktop` was measured as 0 GB and `C:\ProgramData\Docker` was not present.
- Docker engine was healthy during verification (`Server=29.6.1`, storage driver `overlayfs`).

### 6.3 Docker End-to-End Demonstrations

- Baseline smoke run completed successfully (2026-10-12, after the auth-header/digest-attachment fixes):
	- Command mode: non-full-cycle (`-FullCycle:$false`) using `election-demo`
	- Result: successful submission, outbound anchoring, and Transparency snapshot availability
	- Log: `artifacts/validation/smoke-baseline-20261012-160813.log`
- Full-cycle smoke run completed successfully with 200 voters (2026-10-12):
	- Command mode: `-SkipDockerUp -FullCycle -UseFreshElectionId -VoterCount 200`
	- Election ID: `election-demo-cycle-20261012161026`
	- VID example used for verification: `vid_62b2f224f8d87838ce4314419b719721aa75`
	- Final tally: `cand-a=120`, `cand-b=80`
	- Watcher status: `Consistent`
	- Watcher report ID: `84ef3017c93e44939280d07655740f8e`
	- Log: `artifacts/validation/smoke-fullcycle-20261012-161025.log`

Current finalized election id used in the latest full-cycle prototype run:
- `election-demo-cycle-20261012161026`

Current result portal URL:
- `http://localhost:5181/Results?ElectionId=election-demo-cycle-20261012161026`

Current verification URL example:
- `http://localhost:5181/Verify?ElectionId=election-demo-cycle-20261012161026&Vid=vid_62b2f224f8d87838ce4314419b719721aa75`

### 6.4 Kubernetes Scalability Demonstration (5-node cluster)

Observed results in this session:
- Cluster context validated with 5 Ready nodes.
- Metrics pipeline enabled; HPA metrics became available.
- Baseline CPU HPA run remained at 1 replica under spike load (low CPU utilization).
- KEDA lag-based run was then executed as an explicit stress scenario.
- After preparing Kafka lag conditions, outbound scaled from 1 to 3 replicas.

Interpretation:
- CPU-based autoscaling did not trigger for this workload profile.
- Queue-lag autoscaling did trigger under induced backlog pressure, validating elasticity for brokered traffic.

Test-only changes applied during KEDA stress run:
- KEDA installation in namespace `keda`.
- Kafka topic creation for `chainballot-ballots` and backlog injection.
- Temporary high partition count increase to intensify lag signal.

Post-test reset target:
- Delete and recreate namespace `chainballot` from `k8s/chainballot-autoscale-demo.yaml` to restore baseline prototype state.

## 7. Validation Artifacts

Relevant scripts and entry points:
- `scripts/test-all.ps1`
- `docker/e2e-smoke.ps1`
- `docker/seeding/seed-users.ps1`
- `docker-compose.local.yml`
- `artifacts/validation/smoke-baseline-20261012-160813.log` (current baseline evidence, UTF-8 text)
- `artifacts/validation/smoke-fullcycle-20261012-161025.log` (current full-cycle evidence, UTF-8 text)
- `artifacts/validation/smoke-baseline-20260716-120134.log`, `artifacts/validation/smoke-fullcycle-20260716-115825.log` (superseded historical runs, retained for history; predate later additions to the component suites, so their embedded counts/election IDs differ from the current baseline reported above)

These logs were originally captured as UTF-16 `powershell` transcripts and have since been converted to UTF-8 in place for readability/diffability; their content is otherwise unchanged. Regenerate via `docker/e2e-smoke.ps1 *> artifacts/validation/smoke-<scenario>-$(Get-Date -Format 'yyyyMMdd-HHmmss').log` (PowerShell transcripts default to UTF-16; redirect with `*>&1 | Out-File -Encoding utf8` to capture UTF-8 directly) when a fresh run is convenient.

Relevant documentation:
- `README.md`
- `docker/LOCAL_STACK.md`
- `Architecture.md`
- `Prototype.md`

## 8. Summary

This document is the authoritative validation report for the prototype: it records the exact commands run and the exact results observed in a controlled Docker environment, providing reproducible evidence of realistic end-to-end behavior.