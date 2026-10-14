# Chain Ballot — Prototype (Reference Implementation)

## 0. Relationship to Architecture.md

[Architecture.md](Architecture.md) defines the *ideal, technology-agnostic* Chain Ballot framework: architectural roles, protocols, data contracts and trust rules. This document describes **one concrete, running instance** of that framework — the code in this repository — and answers the practical questions Architecture.md deliberately leaves open: which technologies were chosen, what exactly was built, what was deliberately left out, and what evidence exists that it works.

If Architecture.md is "the design", this document is "the build log and the lab report".

## 1. Purpose of the Prototype

This prototype serves **three distinct purposes**, and all three should be kept in mind when reading this document:

1. **Validation artifact.** It demonstrates that every architectural role in Architecture.md has a prototype instance or integration boundary, that every UIS flow (UIS-01…06) has executable evidence, and that the framework's configurability and elasticity mechanisms are observable within the documented claim boundaries.
2. **Open-source contribution.** The repository is published as open source. It is meant to be read, cloned, and evaluated as a contribution in its own right — a coherent, working "base project" for a canonical-ledger-backed election framework, not merely a disposable demo script.
3. **Reusable base project.** Anyone can clone the repository and run the full system locally for testing/demonstration. It is explicitly **not production-ready** (that is out of scope for this phase), but it is intended to be extendable: a future adopter should be able to replace any one prototype instance (e.g., swap CynetAuth for an enterprise identity provider, swap the deterministic-stub prover for a real proof system) without redesigning the framework, because the architecture already models that seam as a pluggable boundary.

> **Prototype ≠ production.** Every "what's missing" statement in this document (§9) is measured against a *production* bar, not against the acceptance bar defined below. The prototype is considered complete for its current phase once the criteria in §7.3 are met.

## 2. Component Instance Map

This table is the authoritative "architecture role → prototype instance" cross-reference used throughout this document and the codebase. Read it alongside [Architecture.md § 2.1](Architecture.md#21-component-catalogue-architectural-roles).

| Architectural role (Architecture.md) | Prototype instance (this repository) | Folder | Primary technology |
| --- | --- | --- | --- |
| Election Configuration Component | `ConfigurationComponent` | [`Configuration/`](Configuration/) | Avalonia desktop app on .NET 9; file-based repositories |
| **Trusted Third Party (TTP) Authentication Service** *(external role)* | **CynetAuth** | [`CynetAuth/`](CynetAuth/) | ASP.NET Core Web API on .NET 9; EF Core + SQLite |
| Election Authorization Component | `ElectionAuthorization.Api` | [`Authorization/`](Authorization/) | ASP.NET Core Web API on .NET 9; EF Core + SQLite |
| Voter Component *(Inbound)* | `VoterComponent.Web` | [`Voter/`](Voter/) | ASP.NET Core Razor Pages on .NET 9 |
| Message Broker / Queue Infrastructure | **Redpanda** (Kafka API-compatible) | `docker-compose.local.yml` service `redpanda` | Redpanda `v24.1.8` container |
| Election Processing Component *(Outbound)* | `ElectionProcessing.Api` + hosted `BackgroundService` | [`ElectionProcessing/`](ElectionProcessing/) | ASP.NET Core Web API on .NET 9 |
| **Canonical Settlement Ledger** *(Smart Contract Platform)* | **Solana** (`solana-test-validator`, local/devnet) | `docker-compose.local.yml` service `solana-validator` | Solana local validator container, `Mock`/`SolanaRpc` adapter modes |
| Transparency & Audit Component | `TransparencyAudit.Api` | [`Transparency/`](Transparency/) | ASP.NET Core Web API on .NET 9; file-backed projection store |
| Result Portal | `ResultPortal.Web` | [`Result/`](Result/) | ASP.NET Core Razor Pages on .NET 9 |
| Watcher / Independent Audit Component | `WatcherAudit.Api` | [`Watcher/`](Watcher/) | ASP.NET Core Web API on .NET 9; file-backed report store |
| Threshold Decryption Orchestration Component | `ThresholdDecryption.Api` | [`Decryption/`](Decryption/) | ASP.NET Core Web API on .NET 9; file-backed ceremony store |
| Election Metadata Repository | Local writable metadata service | `docker-compose.local.yml` service `metadata` | `node:22-alpine` running [`docker/metadata-server.mjs`](docker/metadata-server.mjs); publishes Configuration deployments and serves `docker/metadata/` |
| Shared Public Data Contracts | `ChainBallot.PublicContracts` | [`Shared/`](Shared/) | Plain C# record library (no infrastructure dependency) |

> **Why CynetAuth is called out explicitly:** CynetAuth is **not** an architectural component. It is this prototype's concrete, self-contained instance of the *external* "Trusted Third Party (TTP) Authentication Service" role. It exists so the framework can be demonstrated and tested fully offline, without depending on a real third-party identity provider. A production deployment would replace it with an enterprise IdP; nothing else in the framework would need to change, because every other component only ever depends on the *signed assertion* the TTP role produces, never on CynetAuth specifically.

## 3. Technology Stack Overview

| Concern | Choice in this prototype | Notes |
| --- | --- | --- |
| Runtime | .NET 9 | All API/web components target `net9.0`. |
| Desktop UI | Avalonia | Configuration Component only (cross-platform desktop). |
| Web UI | ASP.NET Core Razor Pages | Voter Component, Result Portal. |
| Web API | ASP.NET Core minimal APIs | Authorization, Processing, Transparency, Watcher, Decryption, CynetAuth. |
| Persistence (identity/authorization) | EF Core + SQLite | CynetAuth, Election Authorization. Schema created automatically via `Database.EnsureCreated()` — no migrations yet (see §9). |
| Persistence (read models / reports / ceremonies) | File-backed JSON stores | Configuration (election drafts, manifests), Transparency (projections), Watcher (reports), Decryption (ceremonies), Voter (submission outbox). |
| Message broker | Redpanda (Kafka wire-protocol compatible) | Canonical runtime transport for ballot submission; an in-memory adapter is retained strictly for unit/component tests. |
| Canonical ledger | Solana (`solana-test-validator`) | Local validator in Docker; `Mock` and `SolanaRpc` adapter modes exist at the Configuration and Processing boundaries. |
| Containerization | Docker Compose, one `Dockerfile.service` template | See §5. |
| Orchestration/scalability demo | Kubernetes (Docker Desktop Kubernetes, kind, or minikube) + HPA + optional KEDA | See §6. |
| Cross-component contracts | Plain C# records, no infrastructure dependency | [`Shared/ChainBallot.PublicContracts`](Shared/ChainBallot.PublicContracts/). |

## 4. Per-Component Implementation Notes

### 4.1 Configuration Component (`ConfigurationComponent`)
- Desktop application (Avalonia/.NET 9); election drafts and deployment manifests are persisted as local JSON files through repository abstractions (no database).
- Metadata is generated as canonical JSON, hashed with SHA-256, and uploaded through an HTTP metadata repository client.
- The Solana interaction boundary has two modes: a `Mock` service (default, deterministic, no network dependency) and a real RPC-backed service — selectable via configuration, so tests and demos never require a live validator unless explicitly configured to.
- Runtime configuration loads from `appsettings.json` plus `CHAINBALLOT_` environment overrides.
- A `RecommendationService` derives suggested consumer/partition/batch-size parameters from expected voter volume, election duration, and peak-participation percentage — this is the concrete realization of Architecture.md §3.1's "scale recommendation model".

### 4.2 CynetAuth (prototype instance of the TTP role)
- ASP.NET Core Web API; SQLite-backed credential store via EF Core.
- Passwords hashed with PBKDF2-SHA256 with a per-user random salt; JWTs carry `sub`, `unique_name`, `amr`, `auth_time` claims.
- Endpoints: `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` (Bearer-protected), `GET /health`.
- Explicitly out of scope for this instance (by design, not by oversight): external social identity providers, multi-factor authentication, enterprise account-recovery flows. A production deployment is expected to *replace* this instance rather than extend it.
- Signing key overridable via `CHAINBALLOT_CYNETAUTH_JWT_SIGNING_KEY`.

### 4.3 Election Authorization Component (`ElectionAuthorization.Api`)
- ASP.NET Core Web API; JWT bearer validation configured against CynetAuth-issued tokens (local signature/issuer validation, no synchronous callback to CynetAuth per request).
- Single SQLite database (EF Core, `EnsureCreated()` at startup) holds VID mappings, per-election authorization state, grants, and audit events.
- One-time grant consumption is enforced with atomic database updates inside a transaction boundary, so concurrent submission attempts cannot double-consume the same grant — validated by a dedicated parallel-consume integration test.
- An admin eligibility-seeding endpoint exists for local provisioning without external systems.
- Trusted-auth signing key overridable via `CHAINBALLOT_AUTHZ_TRUSTEDAUTH_SIGNING_KEY`.

### 4.4 Voter Component (`VoterComponent.Web`, folder `Voter/`)
- Server-rendered ASP.NET Core Razor Pages app; authenticated session stored in an encrypted HTTP-only cookie, with the access token preserved as a claim for downstream API calls.
- Authentication is abstracted behind a local auth-provider client so CynetAuth can be replaced with minimal code change.
- Ballots are encrypted with AES-GCM before submission; the ballot commitment is computed with SHA-256 over the submission bundle.
- The queue abstraction supports Kafka (canonical runtime mode, used against Redpanda) and in-memory (test/local fallback only).
- A **durable, file-backed submission outbox** is part of the normal submission path: the ballot package is staged durably *before* the one-time authorization grant is consumed, the grant is consumed only once staging succeeds, and the entry is only removed after the broker publish is confirmed. A failed publish leaves the already-staged, already-authorized entry for retry; a failed durable-staging attempt aborts before any grant is ever consumed. Configurable TTL, max-retry count and max-capacity policy apply, plus operator endpoints for inspection/manual retry/compaction/removal — this is the concrete mechanism behind Architecture.md §3.4's "submission durability" mitigation and its "no voting right consumed without a durable record" invariant.

### 4.5 Message Broker (Redpanda)
- Runs as a single-node Redpanda container (`docker.redpanda.com/redpandadata/redpanda:v24.1.8`), exposing the Kafka wire protocol on port `9092`.
- Both Voter (producer) and Election Processing (consumer, consumer group `chainballot-outbound`) are configured against the same topic (`chainballot-ballots`).
- Chosen specifically because it is Kafka-API compatible (so the framework's Kafka adapters need no special-casing) while being lightweight enough to run as a single local container.

### 4.6 Election Processing Component (`ElectionProcessing.Api`, folder `ElectionProcessing/`, historically referred to as "Outbound")
- ASP.NET Core Web API with a hosted `BackgroundService` worker.
- Queue ingress/consumption is a configurable adapter: in-memory (tests) or Kafka-backed (canonical runtime).
- Batch closure triggers on either a configurable maximum ballot count or a maximum wait time.
- Merkle-tree and proof generation are strategy abstractions: the default is a binary SHA-256 Merkle tree plus a deterministic-stub proof generator; additional prover strategy modes (`ExternalHttp`, `LocalProcess`) exist behind the same interface (see §9 for production caveats on all of them).
- **Proof artifact verification runs before settlement anchoring** — a batch that fails verification blocks only that election's queue; other elections keep processing. The generic failed-batch retry path re-runs this same verification (against the stored proof artifact and reconstructed batch input) before ever invoking the settlement adapter again, and atomically claims the failed batch so concurrent/duplicate retries cannot double-settle it; a batch that fails verification can never reach settlement, including across repeated retries.
- Ballot ingestion is validated synchronously (structure, VID/commitment/envelope format, canonical nullifier derivation, duplicate-vs-already-anchored detection) before the package is forwarded to the broker, returning an explicit rejection reason instead of silently dropping malformed input later.
- Settlement anchoring supports `Mock` and `SolanaRpc` adapter modes.
- **Important scope note:** `SolanaRpcSettlementAnchor` returns a `SettlementReference` as soon as the RPC submission call completes; it does not poll the validator for transaction confirmation, so the reference's `Commitment` field is the configured label (e.g. `confirmed`), not an observed on-chain status. Architecture.md §3.6's "a batch is only considered settled once the ledger confirms the transaction" rule is therefore not yet enforced by this adapter — see §9.2 item 4 for the concrete gap.
- **Local-validator fallback:** against the unmodified `solana-test-validator` used in the Docker baseline, the custom `chainballot_anchorBatch` program method is not deployed, so the adapter falls back to a deterministic SHA-256 settlement reference computed after resolving the current slot, rather than a signed Anchor-program transaction. This validates the RPC integration boundary and deterministic settlement-reference propagation, but not on-chain execution of batch logic.
- Successfully anchored batches automatically publish projection evidence to the Transparency & Audit Component's ingest endpoint, keeping the read model synchronized without manual intervention.
- Minimal admin endpoints expose status, per-election state, pause/resume, failed-batch retry, and prover readiness. Pause/resume/retry-failed-batch and direct ingest require a role-bound `X-Processing-Auth` header token (roles `processing.operator`, `processing.ingest`); status/state/readiness reads remain public.

### 4.7 Canonical Settlement Ledger instance (Solana)
- Local prototype target is `solana-test-validator` (`solanalabs/solana:v1.18.26`), run with a reset ledger for reproducibility.
- Configuration and Processing both expose a `Mock`/`SolanaRpc` (or `Rpc`) mode switch, so the entire system can run and be tested without a live validator, and can be pointed at one when needed.

### 4.8 Transparency & Audit API (`TransparencyAudit.Api`)
- ASP.NET Core Web API; file-backed projection repository by default.
- Exposes: verified snapshot, VID verification, projection status, canonical-state read, evidence ingestion, mark-stale, reindex, canonical resync event queue, and a full reconciliation workflow (dispute/override/resolve with an immutable case-event trail).
- Authoritative-digest mismatch detection automatically marks a projection stale.
- A configurable authoritative-digest source (`InMemory` or `Http`) and a configurable canonical Solana-read adapter (`ProjectionOnly`, `Mock`, `Http`) allow the read layer to be exercised without a live production backend.
- An `AutoReindexOrchestrator` background worker can periodically reindex configured election ids.

### 4.9 Result Portal (`ResultPortal.Web`, folder `Result/`)
- ASP.NET Core Razor Pages; snapshot and VID-verification pages consume the Transparency API exclusively (no direct repository access).
- Configurable election-visibility policy: `Open`, `HeaderToken`, or `Oidc` authentication modes, with per-election public/private resolution and an option to hide the existence of private elections entirely.

### 4.10 Watcher Component (`WatcherAudit.Api`)
- ASP.NET Core Web API; cross-checks lifecycle, settlement integrity, and metadata/audit digest consistency across the Transparency snapshot/projection/canonical-state endpoints and the authoritative digest source.
- Produces a structured, signed report (digest + signature) with an evidence-export endpoint; reports are persisted file-backed.
- Non-health endpoints require a configured operator access key (`X-Operator-Key` header).

### 4.11 Threshold Decryption Component (`ThresholdDecryption.Api`)
- ASP.NET Core Web API; ceremony lifecycle: propose → governance approvals → trustee key-share submissions → finalize → publish-final-tally, all file-backed persisted.
- **Important scope note:** in this prototype, the *finalize* step's decrypted candidate tallies are supplied as input by the requesting operator/actor, not computed by the API from the encrypted ballots via a live cryptographic threshold-decryption scheme. The component fully implements the *governance orchestration* (independent approvals + independent trustee shares required before finalize is reachable, deterministic result-digest computation over the supplied tallies, and a publication reference), but does not implement the underlying threshold cryptosystem itself. See Architecture.md §3.11 for the architectural framing of this seam, and §9.2.7 below for the concrete gap.
- **Important scope note:** `publish-final-tally` computes a local publication reference (`threshold://{electionId}/{ceremonyId}`) and does not itself call the Transparency & Audit Component's ingest endpoint. In the current prototype, ingestion into Transparency is a separate explicit step performed by the caller (e.g. the e2e smoke script, §6 step 4) rather than being invoked automatically by this component as part of publication. Framework-aligned automatic publish-into-Transparency remains a hardening target — see §9.4.
- Non-health endpoints require a configured operator access key (`X-Operator-Key` header).

### 4.12 Shared Public Contracts (`ChainBallot.PublicContracts`)
- A contracts-only library (no infrastructure dependency) — see [Architecture.md § 2.4](Architecture.md#24-data-model-shared-public-contracts) for the full conceptual data model these types realize.

## 5. Local Docker Environment

### 5.1 Services and Images

| Service | Image | Port | Role |
| --- | --- | --- | --- |
| `redpanda` | `docker.redpanda.com/redpandadata/redpanda:v24.1.8` | `9092` | Message broker |
| `metadata` | `node:22-alpine` running [`docker/metadata-server.mjs`](docker/metadata-server.mjs) (persists to [`docker/metadata/`](docker/metadata/)) | `8088` | Election Metadata Repository (accepts Configuration Component publish requests, serves elections/digests) |
| `cynetauth` | `chainballot-local-cynetauth` (built locally) | `5015` | TTP instance (CynetAuth) |
| `authorization` | `chainballot-local-authorization` | `5147` | Election Authorization Component |
| `voter` | `chainballot-local-voter` | `5231` | Voter Component |
| `outbound` | `chainballot-local-outbound` | `5301` | Election Processing Component |
| `transparency` | `chainballot-local-transparency` | `5264` | Transparency & Audit Component |
| `result` | `chainballot-local-result` | `5181` | Result Portal |
| `watcher` | `chainballot-local-watcher` | `5161` | Watcher Component |
| `decryption` | `chainballot-local-decryption` | `5287` | Threshold Decryption Component |
| `solana-validator` | `solanalabs/solana:v1.18.26` (`solana-test-validator`) | `8899` | Canonical Settlement Ledger instance |

All locally built service images share a single container recipe, [`Dockerfile.service`](Dockerfile.service): a `dotnet/sdk:9.0` build stage (parameterized by `PROJECT_PATH`) publishing into a `dotnet/aspnet:9.0` runtime stage (parameterized by `APP_DLL`). This keeps every component's container image structurally identical and trivially reproducible for a new component.

Configuration is passed entirely through environment variables using the `CHAINBALLOT_<Component>__<Option>` convention (or component-native ASP.NET Core configuration keys such as `Processing__...`), never baked into the image.

### 5.2 Running the Stack

See [`docker/LOCAL_STACK.md`](docker/LOCAL_STACK.md) for the full runbook. In short:

```powershell
docker compose -f docker-compose.local.yml up --build -d
powershell -ExecutionPolicy Bypass -File .\docker\seeding\seed-users.ps1 -UserCount 200 -SeedEligibility
powershell -ExecutionPolicy Bypass -File .\docker\e2e-smoke.ps1
```

### 5.3 Smoke / End-to-End Test Script (`docker/e2e-smoke.ps1`)

This script is the prototype's single-command, reproducible demonstration of the **entire election lifecycle**, and is the primary artifact backing the "end-to-end validated" claim in §8. It:

1. Boots the Docker stack (unless `-SkipDockerUp`) and waits for every service's `/health` endpoint.
2. Seeds demo CynetAuth users and per-election eligibility (unless `-SkipSeeding`).
3. For each seeded user: logs in, resolves the VID profile, requests a one-time voting grant, encrypts and packages a ballot (deterministic `cand-a`/`cand-b` split), publishes it to the Election Processing ingest endpoint, then consumes the authorization grant. This direct-ingest harness confirms durable Kafka hand-off before grant consumption, matching the same "durable record before consumption" invariant the Voter UI workflow now enforces via its own outbox-first staging (see §4.4); the two paths use different durability mechanisms (direct broker publish here vs. local outbox staging in the interactive UI) but the same ordering guarantee.
4. In `-FullCycle` mode (default), waits until Election Processing reports every ballot accepted and anchored, then runs the **full threshold-decryption ceremony**: proposes it, submits governance approvals, submits trustee key shares, finalizes with the actual counted tallies, publishes the final tally, and ingests the finalized projection into Transparency.
5. Waits for the finalized Transparency snapshot, verifies a sample VID resolves as `Verified`, and checks the Result Portal renders the election id and both candidate tallies.

This script is the reference end-to-end realization of UIS-01 through UIS-06. The [`DIAGRAMS/`](DIAGRAMS/) sequence diagrams align with the component workflows they depict.

## 6. Kubernetes Scalability Environment

The prototype includes a separate Kubernetes scalability demonstration under [`k8s/`](k8s/) (full runbook: [`k8s/README.md`](k8s/README.md)). It intentionally keeps a strict separation between:

- **Baseline (optimal) state:** [`k8s/chainballot-autoscale-demo.yaml`](k8s/chainballot-autoscale-demo.yaml) — Redpanda + Election Processing (`outbound`) + a CPU-based `HorizontalPodAutoscaler`. Traffic is generated with [`k8s/spike-ingest.ps1`](k8s/spike-ingest.ps1).
- **Stress-test-only additions:** installing KEDA and applying [`k8s/keda-outbound-kafka-scaledobject.yaml`](k8s/keda-outbound-kafka-scaledobject.yaml) to scale on Kafka consumer lag, plus any temporary topic/partition/backlog manipulation used specifically to cross the lag threshold. These are always explicitly logged as test-only, never presented as baseline behavior.

This separation exists so that scalability evidence gathered under artificially induced backlog is never confused with the framework's default, unmodified runtime behavior — see [`Test Plan and Validation.md` §5.6](Test%20Plan%20and%20Validation.md) for the full baseline-vs-stress reporting rule and observed results.

## 7. Scope of the Prototype

### 7.1 In Scope for This Phase
- Local Docker stack startup and service health for every component.
- Repository structure, buildability, and per-component unit/integration test suites.
- End-to-end ballot submission and anchoring (UIS-01, UIS-02).
- Election close, threshold-decryption ceremony, and final tally publication (UIS-06, first phase).
- Result portal display and individual VID verification (UIS-03, UIS-06 second phase).
- Watcher audit (UIS-05).
- A Kubernetes scalability demonstration (baseline CPU-HPA behavior and a KEDA lag-driven stress scenario), explicitly framed as a demonstration, not a production benchmark.

### 7.2 Out of Scope for This Phase
- Production orchestration and HA/SRE-grade operational guarantees.
- Managed secret-governance platforms (e.g., cloud key vaults).
- External-service SLA validation and provider-level resilience certification.
- Live cryptographic threshold decryption of encrypted ballots (see §4.11 and §9.2.7).
- External tester recruitment and live-host certification.

### 7.3 Prototype Acceptance Criteria
The prototype is considered acceptable for its current phase when all of the following hold:
- UIS-01 through UIS-06 are executable and behavior-aligned with the diagrams in [`DIAGRAMS/`](DIAGRAMS/).
- The canonical settlement ledger remains the documented authority for settlement/finalization state.
- Cross-component contracts (§2.4 of Architecture.md) are stable enough for reproducible integration.
- Default unit/component test suites run deterministically without any required external service.
- Non-health operational endpoints enforce at least baseline operator access control.
- Missing production concerns are explicitly documented (§9), never silently implied complete.
- A new developer can reproduce a coherent local run with minimal manual steps (§5.2).

## 8. Validation and Test Evidence

The authoritative, detailed validation report — including exact commands, exact observed results, and reproducible log artifacts — is [`Test Plan and Validation.md`](Test%20Plan%20and%20Validation.md). This section summarizes the evidence categories; see that document for full tables and raw evidence.

### 8.1 Unit and Integration Test Suites
Every component solution has its own xUnit test project, runnable individually or all at once via [`scripts/test-all.ps1`](scripts/test-all.ps1). Latest full run: **157 tests passed** across 9 suites (Configuration, CynetAuth, Authorization, Voter, Election Processing, Transparency, Result, Watcher, Decryption) — see `Test Plan and Validation.md § 5.0` and `§ 6.1` for the per-suite breakdown.

### 8.2 End-to-End Docker Validation
[`docker/e2e-smoke.ps1`](docker/e2e-smoke.ps1) (§5.3 above) exercises the full election lifecycle, including a 200-voter full-cycle run with threshold decryption, final tally publication, Transparency finalization, VID verification, and Watcher audit. Reproducible log artifacts: [`artifacts/validation/smoke-baseline-20260716-120134.log`](artifacts/validation/smoke-baseline-20260716-120134.log), [`artifacts/validation/smoke-fullcycle-20260716-115825.log`](artifacts/validation/smoke-fullcycle-20260716-115825.log).

### 8.3 Kubernetes Scalability Demonstration
Executed on a 5-node Kubernetes cluster: a baseline CPU-HPA run did not scale under the tested load profile (informative negative result — CPU is not always the right signal for a queue-decoupled workload), while a KEDA Kafka-lag–driven run scaled the processing deployment from 1 to 3 replicas under an explicitly logged, induced backlog condition. See `Test Plan and Validation.md § 5.5`, `§ 5.6` and `§ 6.4` for the full baseline-vs-stress breakdown and interpretation, and [`k8s/README.md`](k8s/README.md) for the runbook.

## 9. Scope and Known Gaps

This section is intentionally stricter than Architecture.md. If something is listed here, it means one of the following is true for *this prototype instance*: it does not exist yet, it exists only as a scaffolded/adapter seam, it is implemented but not yet durable across process restarts, it depends on default/mock runtime behavior, or it is documented more strongly elsewhere than the code can currently guarantee. None of these gaps affect the acceptance criteria in §7.3 — they are production-hardening items.

### 9.1 Components with partial (not "missing") implementation

All read-layer and post-close components below are **implemented and tested**; the gaps are specifically about production-grade external integration, not about missing functionality.

**Transparency & Audit API** — implemented: real ASP.NET Core service, snapshot/verification endpoints, file-backed projection repository, evidence ingestion, reindex/mark-stale, deterministic consistency digests, configurable authoritative-digest source (`InMemory`/`Http`), automated reindex orchestration, canonical Solana read adapter (`ProjectionOnly`/`Mock`/`Http`), canonical resync event queue, and a full reconciliation workflow (dispute/override/resolve) with immutable case events. Still missing: live production Solana endpoint/credential integration, live production metadata/audit repository correlation, and institutionalized governance policy for reconciliation outcomes.

**Result Portal** — implemented: real Razor Pages portal, snapshot and VID-verification pages, configurable auth modes (`Open`/`HeaderToken`/`Oidc`), per-election visibility enforcement, and integration tests covering private-election access control and OIDC challenge behavior. Still missing: end-to-end validation against a *live* Transparency API host and a *live* enterprise identity provider deployment (current OIDC behavior is config-driven and unit/integration-tested, not live-host tested).

**Watcher Component** — implemented: real API, cross-checks across snapshot/projection/canonical-state/digest source, structured signed reports, evidence export, file-backed persistence, unit/integration tests. Still missing: live production provider integration, production key-governance for signing keys, a dashboard UI (current interface is API-only), and live-host end-to-end validation.

**Threshold Decryption Component** — implemented: real API, full ceremony lifecycle (propose/approve/share/finalize/publish), file-backed persistence, unit/integration tests, and a canonical closure precondition check (ceremony proposal and finalisation both call Transparency's canonical-state endpoint and fail closed unless it reports finalized). Still missing (beyond the governance-vs-cryptography scope note in §4.11): a distinct network-reachable Closed-vs-Finalized canonical signal (today only one boolean canonical signal exists anywhere in the system, used as the closure gate — see §9.4), production key-custody/share governance, and live-host end-to-end validation.

### 9.2 Non-Production Runtime Seams

1. **Inbound queue publisher** — Kafka is the default and canonical runtime transport; in-memory is retained only for unit/component tests. Remaining: operational dead-letter/replay governance and retention tuning for a production Kafka cluster.
2. **Inbound retry outbox** — durable, file-backed, with TTL/max-retry/capacity policy and operator endpoints. Remaining: queue-backed exactly-once semantics and a production operator dashboard beyond the API.
3. **Outbound queue adapters** — in-memory and Kafka-backed adapters exist. Remaining: real deployed cluster configuration, topic/retention policy, consumer-group operational procedures, and live-broker failure/replay validation.
4. **Outbound settlement adapters** — `Mock` and `SolanaRpc` modes exist. Remaining: validation against a live Solana cluster with real program ids/payer keys, and chain-side reconciliation/observability for failed submissions. The `SolanaRpc` adapter also does not yet poll for transaction confirmation before returning a `SettlementReference` (see §9.4 and §4.6's scope note) — it currently treats RPC-submission success as settlement, rather than an observed on-chain confirmation.
5. **Outbound prover modes** — deterministic stub, external HTTP, and local-process modes exist. Remaining: a real production proof system as the default path, verified external-prover SLAs, and canonical proof-format governance.
6. **Proof verification** — artifact verification runs before anchoring, but is not yet a full cryptographic verification pathway with a canonical verifier integration.
7. **Configuration Solana deployment** — `Mock` and RPC-backed modes exist. Remaining: fully operational production deployment configuration, key-management hardening, and end-to-end production proof of the RPC path.

### 9.3 Data Model Gaps (Not Missing Code, But Not Yet Frozen as Production Contracts)

- **Nullifier/replay rule** — CLOSED for this phase: the canonical derivation (`SHA256("{ElectionId}|{Vid}|{SubmissionNonce}")`, lower-case hex) is frozen in both the shared contract documentation and in Outbound's enforcement logic.
- **Tally-state schema** — runtime-local counters exist with a frozen schema *version* (`TallyRecordSchemaVersion = 1`); a real on-chain tally-state account model is still open.
- **Batch state schema** — batch id/sequence/root/proof/settlement fields exist with a frozen schema *version* (`BatchRecordSchemaVersion = 1`); the exact on-chain account layout is still open.
- **Proof format and verification path** — strategy-based generation and basic verification exist with a frozen schema *version* (`ProofArtifactSchemaVersion = 1`); the locked proof format and on-chain-vs-referenced verification decision are still open.

### 9.4 Correctness and Resilience Gaps

- **Inbound submission durability** — CLOSED for this phase: the durable outbox is part of the normal submission path (staged before the grant is consumed, marked authorized before publish is attempted, removed only after publish is confirmed), survives process restarts, and retains failed publications for retry; recovery of a staged-but-not-yet-authorized entry re-attempts grant consumption before publish. Remaining: queue-backed exactly-once semantics and production Kafka governance (retention, DLQ, replay runbooks) beyond this ordering guarantee.
- **Outbound retry proof re-verification** — CLOSED for this phase: the generic failed-batch retry path re-verifies the stored proof artifact against a reconstructed batch input before ever invoking the settlement adapter, and atomically claims the failed batch so a batch whose proof fails verification can never reach settlement, including across concurrent or repeated retries.
- **Outbound/Transparency operational route authorization** — CLOSED for this phase: Election Processing's pause/resume/retry-failed-batch/ingest routes and Transparency's ingestion/mark-stale/reindex/resync/reconciliation routes require a role-bound header-token credential (`X-Processing-Auth`, `X-Transparency-Auth`), extending the same shared-secret pattern already used by Watcher/Decryption. Remaining: production identity federation and role-based policy beyond a shared header key (same remaining scope as the existing Watcher/Threshold item below).
- **Transparency stale/canonical-mismatch verification** — CLOSED for this phase: VID verification and the verified snapshot both fail closed (return `Inconsistent`/404 rather than `Verified`) when the projection is marked stale, or when a configured canonical read source contradicts a projection claiming `Finalized`. A `ProjectionOnly` deployment (no canonical read source configured) relies on the staleness check alone.
- **Decryption canonical-Closed precondition** — CLOSED for this phase, with a documented prototype simplification: ceremony proposal and finalisation both query Transparency's canonical-state endpoint and fail closed (reject) unless it reports the election finalized, or if the endpoint is unreachable/malformed. No component in this system exposes a distinct "Closed-but-not-yet-Finalized" signal over the network today, so the single available boolean is used as the gate; this is documented in code and should not be read as claiming a richer canonical lifecycle model than actually exists end-to-end.
- **Settlement/finalisation idempotency** — CLOSED for this phase: a batch that has already settled cannot be re-settled by a subsequent or concurrent retry (the failed-batch claim is exclusive); ceremony finalisation/publication already returned the same digest/reference on repeated calls and this is now covered by automated tests.
- **Settlement confirmation** — `SolanaRpcSettlementAnchor` returns a `SettlementReference` immediately after the RPC submission call succeeds; it does not wait for or verify on-chain transaction confirmation before the batch is treated as settled (Architecture.md §3.6). Framework-aligned confirmation-gated settlement remains a hardening target.
- **Decryption-to-Transparency publication** — `ThresholdDecryption.Api`'s `publish-final-tally` step produces a local publication reference only; it does not call the Transparency & Audit Component's ingest endpoint itself. Framework-aligned automatic publication remains a hardening target; today this handoff is performed as a separate explicit step by the caller (see §6 step 4).
- **In-memory queue mode** — intentionally retained only as a test fallback; remaining work is guardrails to prevent accidental non-test use.
- **Outbound failure isolation** — implemented (only the affected election's queue is blocked); not yet validated under live-cluster chaos/failure testing.
- **Watcher / Threshold operator access control** — CLOSED for this phase: both components' non-health endpoints require a configured operator access key, with tests covering authorized execution. Remaining: production identity federation and role-based policy beyond a shared header key.

## 10. Reproducibility Quickstart

- Clone the repository, then run [`scripts/test-all.ps1`](scripts/test-all.ps1) to execute every component's test suite without any external dependency.
- Run the full local election lifecycle with Docker: [`docker/LOCAL_STACK.md`](docker/LOCAL_STACK.md) → [`docker/e2e-smoke.ps1`](docker/e2e-smoke.ps1) (§5 above).
- Run the Kubernetes scalability demonstration: [`k8s/README.md`](k8s/README.md) (§6 above).
- For the full, authoritative validation report (exact commands, exact observed results): [`Test Plan and Validation.md`](Test%20Plan%20and%20Validation.md).
- For the conceptual framework this prototype implements: [`Architecture.md`](Architecture.md).
