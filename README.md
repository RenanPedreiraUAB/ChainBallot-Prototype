# Chain Ballot

Chain Ballot is a multi-component election platform designed around a canonical Solana settlement layer, verifiable transparency, and component separation between configuration, voting, processing, results, and auditing.

This repository is organized as the root `ChainBallot` workspace.

## Documentation Map

Start here. This repository's documentation is deliberately split into two complementary documents, plus one validation report:

| Document | Read this for |
| --- | --- |
| **[Architecture.md](Architecture.md)** | The conceptual, technology-agnostic *framework design*: architectural roles, protocols, data contracts, trust/threat model, smart-contract and blockchain interaction model. Start here to understand *why* the system is shaped this way. |
| **[Prototype.md](Prototype.md)** | The concrete *reference implementation*: technology choices, component-instance naming map (e.g. which prototype instance plays the "TTP Authentication" role), Docker/Kubernetes environments, explicit scope, and known gaps. Start here to understand *how this codebase realizes* the architecture. |
| **[Test Plan and Validation.md](Test%20Plan%20and%20Validation.md)** | The authoritative validation report: exact commands, exact observed results, and reproducible evidence logs for unit/integration/e2e/scalability testing. |
| **[docs/Manual-E2E-Testing-Guide.md](docs/Manual-E2E-Testing-Guide.md)** | A guided, click-through tutorial for manually testing the full election lifecycle yourself: configure an election, vote as different users, view results, and check the transparency/watcher audit trail. Start here if you want to try the system hands-on. |
| **[docs/Traceability-Matrix.md](docs/Traceability-Matrix.md)** | Cross-reference from architecture roles and UIS flows to concrete implementation folders, endpoints, and validation IDs. |
| **[docs/API-Reference.md](docs/API-Reference.md)** | Central API route index with base URLs, auth expectations, and key endpoint groups. |
| **[docs/Configuration-Reference.md](docs/Configuration-Reference.md)** | Consolidated runtime configuration and environment variable reference for local prototype execution. |
| **[docs/Diagrams-Guide.md](docs/Diagrams-Guide.md)** | Diagram catalogue and consistency rules for the UIS and logical architecture figures under `DIAGRAMS/`. |
| **[docs/Repository-Structure.md](docs/Repository-Structure.md)** | Canonical repository navigation map, alias clarification, and structure cleanup guidance. |
| **[CONTRIBUTING.md](CONTRIBUTING.md)** | Contribution workflow, validation expectations, and documentation update rules. |
| **[SECURITY.md](SECURITY.md)** | Security reporting and coordinated handling policy. |
| **[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)** | Collaboration behavior and enforcement expectations. |
| **[LICENSE](LICENSE)** | Repository license terms. |
| **[VERSIONS.md](VERSIONS.md)** | Single-page reference of every pinned dependency/runtime/container version used by the prototype. |

## Claims Boundaries

Claims this repository makes:
- The architecture is implemented as a coherent, runnable prototype with explicit role boundaries.
- UIS-01 through UIS-06 are executable and represented in the diagrams under `DIAGRAMS/`.
- Unit/integration suites and local end-to-end validation are reproducible.
- Kubernetes scalability behavior is documented with strict baseline-vs-stress separation.

Claims this repository does not make:
- Production readiness, HA/SRE hardening, or cloud deployment certification.
- Live enterprise identity/provider integration certification.
- Full production cryptographic threshold decryption implementation.
- Formal performance benchmarking beyond documented prototype-scale demonstrations.

## Ordered Quickstart

1. Validate toolchain prerequisites: Docker Desktop, .NET 9 SDK, and PowerShell.
2. Run component test suites:

```powershell
Set-Location D:\GitHub\ChainBallot
powershell -ExecutionPolicy Bypass -File .\scripts\test-all.ps1
```

Expected outcome: all suites pass (current baseline: 157 tests).

3. Start the local stack:

```powershell
docker compose -f docker-compose.local.yml up --build -d
```

Expected outcome: all services are running with healthy endpoints.

4. Seed demo users and eligibility:

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\seeding\seed-users.ps1 -UserCount 200 -SeedEligibility
```

Expected outcome: users are created (or already present) and eligibility is seeded.

5. Execute the full election lifecycle smoke run:

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\e2e-smoke.ps1
```

Expected outcome: ballots are submitted and anchored, final tally is published, transparency is finalized, and watcher audit evidence is produced.

6. Validate user-facing outputs:
- Results: `http://localhost:5181/Results?ElectionId=<election-id>`
- VID verification: `http://localhost:5181/Verify?ElectionId=<election-id>&Vid=<vid>`

Expected outcome: finalized results and successful VID verification for valid entries.

7. (Optional) Run Kubernetes scalability demonstration using [k8s/README.md](k8s/README.md), reporting baseline and stress evidence separately.

Prefer to click through the system yourself instead of running an automated script? Follow [docs/Manual-E2E-Testing-Guide.md](docs/Manual-E2E-Testing-Guide.md) for a guided, hands-on walkthrough of the same lifecycle.

For detailed runbooks and route/config references, use the documentation map above.

## Repository Layout

Every top-level directory and root file in this repository, for quick navigation:

| Path | Contents |
| --- | --- |
| `Configuration/` | Election Configuration Component — Avalonia desktop app and tests |
| `CynetAuth/` | Trusted Third Party (TTP) Authentication Service prototype instance — API and tests |
| `Authorization/` | Election Authorization Component — API and tests |
| `Voter/` | Voter Component (historical alias: Inbound) — Razor Pages ballot workspace and tests |
| `ElectionProcessing/` | Election Processing Component (historical alias: Outbound) — API, background worker, and tests |
| `Transparency/` | Transparency & Audit Component — API and tests |
| `Result/` | Result Portal — web app and tests |
| `Watcher/` | Watcher / Independent Audit Component — API and tests |
| `Decryption/` | Threshold Decryption Orchestration Component — API and tests |
| `Shared/` | `ChainBallot.PublicContracts` — cross-component data contracts |
| `docker/` | Local Docker stack assets: `LOCAL_STACK.md`, `e2e-smoke.ps1`, `metadata/`, `seeding/` |
| `k8s/` | Kubernetes scalability demonstration manifests and scripts |
| `DIAGRAMS/` | Rendered UIS and component sequence diagrams (SVG) backing `Architecture.md` |
| `DIAGRAMS_CODE/` | Editable diagram sources (Mermaid `.mmd` / WebSequenceDiagrams `.txt`) — source-of-truth for every SVG under `DIAGRAMS/` |
| `docs/` | Secondary reference docs (traceability, API, configuration, diagrams, structure) |
| `scripts/` | Repository-wide automation (`test-all.ps1`) |
| `artifacts/validation/` | Retained Docker smoke-run evidence logs |
| `.github/` | GitHub Actions CI workflow(s) |
| `docker-compose.local.yml` | Local multi-service Docker Compose stack definition |
| `Dockerfile.service` | Shared multi-stage build template for every .NET service container |
| `.gitignore` / `.dockerignore` | Build-artifact and Docker build-context exclusion rules |
| `.editorconfig` | Shared formatting conventions (indentation, charset, C# style) across all components |

Doc files (`Architecture.md`, `Prototype.md`, `Test Plan and Validation.md`, `LICENSE`, and the rest) are indexed in the Documentation Map above.

For canonical navigation paths and nested-folder clarification, see [docs/Repository-Structure.md](docs/Repository-Structure.md).

## Validation Evidence

The authoritative validation report for the prototype is maintained in `Test Plan and Validation.md`.

Latest reproducible local evidence set (2026-10-12):
- Repository-wide tests via `scripts/test-all.ps1` (157 passed tests across all component suites)
- Baseline Docker smoke run log: `artifacts/validation/smoke-baseline-20261012-160813.log`
- Full-cycle 200-voter run log: `artifacts/validation/smoke-fullcycle-20261012-161025.log`
- Finalized result portal sample: `http://localhost:5181/Results?ElectionId=election-demo-cycle-20261012161026`

This section is intended as a quick index; detailed scenarios and observed outcomes are documented in the validation markdown report.

## Architecture Baseline

The architecture for this repository is documented in [Architecture.md](Architecture.md). In short:

- Solana is the canonical authority for settlement state.
- The write path is separated from the read path.
- Configuration initializes reusable election program instances rather than generating per-election contract code.
- Transparency & Audit views must reconcile against canonical chain references.
- Threshold decryption happens after election close.

The current implementation is a technically coherent subset of that target architecture:

- Configuration is Avalonia/.NET 9 and persists election drafts as local JSON files.
- CynetAuth is an ASP.NET Core/.NET 9 API with EF Core + SQLite credential storage.
- Election Authorization is an ASP.NET Core/.NET 9 API with EF Core + SQLite state for VID mappings, eligibility, grants, and audit events.
- Voter Component (historical alias: Inbound) is a server-rendered ASP.NET Core/.NET 9 Razor Pages app with cookie auth, AES-GCM ballot encryption, and Kafka-first queue publishing.
- Election Processing Component (historical alias: Outbound) is an ASP.NET Core/.NET 9 API with a `BackgroundService` worker, strategy-based tree/prover abstractions, and Solana-RPC local-validator settlement as the canonical local runtime path.
- Shared contracts provide the public DTO layer for cross-component handoff.

## Current Implementation Status

Configuration is implemented end-to-end, and Shared public contracts are now established.

Implemented in `CynetAuth/`:

- Secure registration/login API with SQLite-backed credentials
- Password hashing via PBKDF2 with per-user salt
- JWT issuance for downstream authentication middleware integration
- Authenticated identity endpoint and integration tests
- Runtime connection string via `ConnectionStrings:AuthDb`, with local fallback to `Data Source=cynetauth.db`
- JWT signing key override via `CHAINBALLOT_CYNETAUTH_JWT_SIGNING_KEY`

Implemented in `Authorization/`:

- Trusted JWT validation boundary against CynetAuth
- VID creation/retrieval for authenticated subjects
- Election eligibility and vote-consumption state repository model
- One-time grant issue/consume API with atomic consume semantics
- Parallel-consume integration test proving single successful vote consumption
- Runtime connection string via `ConnectionStrings:AuthorizationDb`, with local fallback to `Data Source=election-authorization.db`
- Admin eligibility seeding endpoint for local setup
- Trusted-auth signing key override via `CHAINBALLOT_AUTHZ_TRUSTEDAUTH_SIGNING_KEY`
- Local audit-event storage for VID, grant, eligibility, and consume events

Implemented in `Configuration/ConfigurationComponent`:

- Election draft creation and editing
- Candidate management
- Validation rules
- Metadata generation and hashing
- Deployment manifest generation
- Local election persistence
- Mock and RPC Solana service boundaries
- Avalonia desktop UI
- Unit and integration tests
- File-based repositories for election drafts and deployment manifests
- HTTP metadata upload repository for published election metadata
- Runtime configuration loaded from `appsettings.json` plus `CHAINBALLOT_` environment overrides

Implemented in `Voter/VoterComponent`:

- Server-rendered Razor Pages ballot workspace
- Auth-provider abstraction with CynetAuth login integration
- Encrypted HTTP-only cookie session for signed-in voters
- Election authorization profile and state lookup
- Metadata loading for ballot rendering when available
- AES-GCM ballot encryption and SHA-256 commitment computation
- Kafka queue publisher as the current canonical queue boundary
- Durable file-backed submission outbox, staged as part of the normal submission path before the one-time authorization grant is consumed (not merely a failure fallback)
- Outbox policy controls for retention TTL, max retries, and max capacity
- Authenticated operator endpoints for outbox state, manual retry, compaction, and targeted removal
- Integration tests covering login and ballot submission

Implemented in `ElectionProcessing/ElectionProcessing`:

- ASP.NET Core Web API host with background processing worker
- Queue ingress/consumer abstraction with Kafka as canonical runtime mode (in-memory retained for tests/fallback)
- Batch closure on configurable count and max-wait thresholds
- Strategy abstractions for Merkle tree and ZK proof generation
- Default binary SHA-256 tree strategy
- Default deterministic ZK stub strategy (replaceable by real prover implementation)
- Additional LocalProcess and ExternalHttp prover strategy modes are available via configuration
- Proof artifact verification is executed before settlement anchoring
- Settlement anchor abstraction with Solana-RPC local-validator runtime mode (mock remains available as fallback/test mode)
- Per-election failure blocking semantics with retry endpoint for failed batches
- Automatic anchored-batch projection evidence publishing to Transparency ingest endpoint
- Minimal admin endpoints for status, election state, pause/resume, and failed-batch retry
- Unit tests for batch closure rules and per-election blocking behavior

Implemented in `Transparency/TransparencyAudit`:

- ASP.NET Core/.NET 9 Transparency and Audit API as read-model boundary
- Verified election snapshot endpoint returning canonical `VerifiedResultSnapshot`
- VID verification endpoint returning canonical `VidVerificationResponse`
- Projection repository abstraction with default file-backed projection storage
- Integration tests for verified, not-found, and election-not-finalized verification outcomes

Implemented in `Result/ResultPortal`:

- ASP.NET Core/.NET 9 Razor Pages result presentation component
- Verified snapshot page consuming Transparency API contracts
- VID verification page consuming Transparency API contracts
- Configurable HTTP client boundary for Transparency API base URL and timeouts
- Configurable election visibility policy with `Open`, `HeaderToken`, and `Oidc` authentication modes
- Unit tests validating snapshot and verification client behavior

Implemented in `Watcher/WatcherAudit`:

- ASP.NET Core/.NET 9 Watcher audit API for election consistency supervision
- Cross-checks across transparency snapshot/projection/canonical-state and authoritative digest sources
- Structured inconsistency reports with check details and operator summary endpoint
- Evidence export endpoint with digest plus signature
- File-backed report persistence and retrieval endpoints
- Unit and integration tests for watcher execution and endpoint behavior

Implemented in `Decryption/ThresholdDecryption`:

- ASP.NET Core/.NET 9 threshold decryption orchestration API
- Governance approval workflow and trustee share collection workflow
- Decryption finalization with deterministic result digest generation
- Final tally publication artifact generation endpoint
- File-backed ceremony persistence and retrieval endpoints
- Unit and integration tests for ceremony lifecycle behavior

Planned folders listed above are tracked as architecture targets and are intentionally implemented one component at a time.

Implemented in `Shared/ChainBallot.PublicContracts`:

- Canonical `SettlementReference` contract
- `InclusionProofReference` contract
- `BatchAnchorReference` contract
- `BallotSubmissionPackage`, `EncryptedBallotPayload`, and `QueueReceipt` handoff contracts
- `VidVerificationResponse` and verification outcome contract
- `VerifiedResultSnapshot` contract

This contract layer exists to keep smart-contract/read-model handoff assumptions public and centralized.

### Technical Notes

- CynetAuth and Election Authorization both create their local SQLite schemas automatically at startup with `Database.EnsureCreated()` for local convenience.
- The current repository does not contain EF migrations for these databases yet.
- Configuration does not use a database for election drafts or deployment artifacts; it uses local files and published metadata endpoints.
- Transparency, result portal, and watcher now have implemented runtime services.
- The Voter Component now uses Kafka as the canonical runtime queue boundary; in-memory mode is retained only for tests/fallback.

## Configuration Component

The Configuration component is an Avalonia desktop application targeting .NET 9. It is the part of the system used to provision elections, generate metadata, compute metadata hashes, and deploy election configuration artifacts.

It reads configuration from `appsettings.json` and `CHAINBALLOT_` environment variables, then uses file repositories for election state and deployment artifacts.

### Run the app

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
 dotnet run --project .\Configuration\ConfigurationComponent\ConfigurationComponent.csproj --no-restore
```

If restore is available in your environment, the following also works:

```powershell
dotnet run --project .\Configuration\ConfigurationComponent\ConfigurationComponent.csproj
```

### Run tests

The repository currently runs cleanly with the no-restore path in this environment:

```powershell
Set-Location D:\GitHub\ChainBallot
 dotnet test .\Configuration\ConfigurationComponent.sln --no-restore
```

If your local environment can restore packages without policy interference, you can also run:

```powershell
dotnet test .\Configuration\ConfigurationComponent.sln
```

### Build

```powershell
Set-Location D:\GitHub\ChainBallot
 dotnet build .\Configuration\ConfigurationComponent.sln --no-restore
```

## CynetAuth Component

The CynetAuth component is the trusted third-party identity boundary for the current implementation. It validates local credentials, issues JWTs, and stores its user records in SQLite through Entity Framework Core.

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
 dotnet run --project .\CynetAuth\CynetAuth.Api\CynetAuth.Api.csproj --no-restore
```

Run tests:

```powershell
Set-Location D:\GitHub\ChainBallot
 dotnet test .\CynetAuth\CynetAuth.sln --no-restore
```

## Election Authorization Component

The Election Authorization component consumes CynetAuth JWTs, maps authenticated subjects to VIDs, tracks election eligibility, and enforces one-time authorization consumption in SQLite-backed repositories.

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Authorization\ElectionAuthorization.Api\ElectionAuthorization.Api.csproj --no-restore
```

Run tests:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Authorization\ElectionAuthorization.sln --no-restore
```

## Voter Component (historical alias: Inbound)

The Voter Component is a server-rendered Razor Pages app that signs users in through CynetAuth, resolves election state from Election Authorization, loads metadata when available, encrypts ballot submissions, and publishes them to Kafka as the canonical runtime queue.

### Run the app

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Voter\VoterComponent\VoterComponent.Web\VoterComponent.Web.csproj --no-restore
```

### Run tests

```powershell
Set-Location D:\GitHub\ChainBallot
 dotnet test .\Voter\VoterComponent\VoterComponent.sln --no-restore
```

## Election Processing Component (historical alias: Outbound)

The Election Processing component is an ASP.NET Core Web API plus `BackgroundService` worker that consumes ballot packages from Kafka in canonical runtime mode (with in-memory retained for tests/fallback), creates batches, computes Merkle roots, generates proof artifacts through a strategy abstraction, and anchors settlement references through a replaceable anchor adapter.

### Run the app

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\ElectionProcessing\ElectionProcessing\ElectionProcessing.Api\ElectionProcessing.Api.csproj --no-restore
```

### Run tests

```powershell
Set-Location D:\GitHub\ChainBallot
 dotnet test .\ElectionProcessing\ElectionProcessing\ElectionProcessing.sln --no-restore
```

## Transparency / Audit API Component

The Transparency component serves verified read models to downstream presentation and audit layers.

### Run the app

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Transparency\TransparencyAudit.Api\TransparencyAudit.Api.csproj --no-restore
```

### Run tests

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Transparency\TransparencyAudit.sln --no-restore
```

## Result Portal Component

The Result Portal component is an ASP.NET Core Razor Pages app that consumes Transparency read endpoints for verified snapshots and VID verification views.

### Run the app

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Result\ResultPortal.Web\ResultPortal.Web.csproj --no-restore
```

### Run tests

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Result\ResultPortal.sln --no-restore
```

## Watcher Component

The Watcher component is an ASP.NET Core Web API that runs audit consistency checks and emits signed evidence reports for election supervision.

### Run the app

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Watcher\WatcherAudit.Api\WatcherAudit.Api.csproj --no-restore
```

### Run tests

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Watcher\WatcherAudit.sln --no-restore
```

## Local E2E User Seeding

After local docker stack startup, seed 200 demo users (and optional election eligibility) with:

```powershell
Set-Location D:\GitHub\ChainBallot
powershell -ExecutionPolicy Bypass -File .\docker\seeding\seed-users.ps1 -UserCount 200 -SeedEligibility
```

## Threshold Decryption Component

The Threshold Decryption component is an ASP.NET Core Web API that coordinates post-close ceremony approvals, trustee shares, decryption finalization, and final tally publication artifacts.

### Run the app

From the repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Decryption\ThresholdDecryption.Api\ThresholdDecryption.Api.csproj --no-restore
```

### Run tests

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Decryption\ThresholdDecryption.sln --no-restore
```

## Validation

For a full local validation pass, run [scripts/test-all.ps1](scripts/test-all.ps1). It executes the test suite for Configuration, CynetAuth, Authorization, Voter, ElectionProcessing, Transparency, Result, Watcher, and Decryption in sequence.

GitHub Actions uses [.github/workflows/dotnet-ci.yml](.github/workflows/dotnet-ci.yml) to restore and test every component solution on push and pull request events.

## Local Docker Stack

A local multi-service Docker stack is provided for prototype validation.

Quick start:

```powershell
Set-Location D:\GitHub\ChainBallot
docker compose -f .\docker-compose.local.yml up --build -d
```

Optional local Solana validator profile:

```powershell
Set-Location D:\GitHub\ChainBallot
docker compose -f .\docker-compose.local.yml --profile solana up --build -d
```

Stack details and notes:

- [docker/LOCAL_STACK.md](docker/LOCAL_STACK.md)

Run full-cycle Docker smoke with configurable voter volume:

```powershell
Set-Location D:\GitHub\ChainBallot
.\docker\e2e-smoke.ps1 -VoterCount 500 -TimeoutSeconds 600
```

## Kubernetes Autoscaling Demo

An optional Kubernetes demonstration is provided to show how inbound-broker-outbound traffic can scale during workload spikes.

For reporting clarity, keep baseline and stress-test states separate:
- baseline/optimal state and reset flow: [k8s/README.md](k8s/README.md)
- validation interpretation and test-only modifications log: [Test Plan and Validation.md](Test%20Plan%20and%20Validation.md)

- [k8s/README.md](k8s/README.md)
- [k8s/chainballot-autoscale-demo.yaml](k8s/chainballot-autoscale-demo.yaml)
- [k8s/keda-outbound-kafka-scaledobject.yaml](k8s/keda-outbound-kafka-scaledobject.yaml)
- [k8s/spike-ingest.ps1](k8s/spike-ingest.ps1)

## Notes

- The repository root is the outer `ChainBallot` folder.
- Build outputs (`bin/` and `obj/`) are ignored by git.
- Configuration, CynetAuth, Election Authorization, Voter/VoterComponent, ElectionProcessing/ElectionProcessing, Transparency/TransparencyAudit, Result/ResultPortal, Watcher/WatcherAudit, and Decryption/ThresholdDecryption are currently implemented runtime components at this stage.
- Shared public contracts are established to support cross-component consistency.
