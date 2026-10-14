# Chain Ballot API Reference (Central Index)

This file is a central route index for integration and test automation. It intentionally lists key operational routes and does not replace source-level endpoint definitions.

Source of truth for route registration:
- `CynetAuth/CynetAuth.Api/Endpoints/AuthEndpoints.cs`
- `Authorization/ElectionAuthorization.Api/Endpoints/ElectionAuthorizationEndpoints.cs`
- `Voter/VoterComponent/VoterComponent.Web/Endpoints/OutboxEndpoints.cs`
- `ElectionProcessing/ElectionProcessing/ElectionProcessing.Api/Endpoints/ElectionProcessingEndpoints.cs`
- `Transparency/TransparencyAudit.Api/Endpoints/TransparencyEndpoints.cs`
- `Watcher/WatcherAudit.Api/Endpoints/WatcherEndpoints.cs`
- `Decryption/ThresholdDecryption.Api/Endpoints/ThresholdEndpoints.cs`

## 1. Local Base URLs (Docker)

| Component | Base URL |
| --- | --- |
| CynetAuth | `http://localhost:5015` |
| Election Authorization | `http://localhost:5147` |
| Voter Component | `http://localhost:5231` |
| Election Processing | `http://localhost:5301` |
| Transparency API | `http://localhost:5264` |
| Result Portal | `http://localhost:5181` |
| Watcher API | `http://localhost:5161` |
| Threshold Decryption API | `http://localhost:5287` |

## 2. Authentication and Authorization Conventions

| Convention | Applies to |
| --- | --- |
| JWT bearer token (from CynetAuth) | Election Authorization protected routes |
| Cookie/session auth (Razor pages) | Voter and Result web surfaces |
| `X-Operator-Key` header | Watcher and Threshold Decryption protected routes |
| `X-Processing-Auth` header (role claims: `processing.operator`, `processing.ingest`) | Election Processing operational routes (pause/resume/retry-failed-batch/ingest) |
| `X-Transparency-Auth` header (role claims: `transparency.ingestion`, `transparency.reconciliation`) | Transparency ingestion/maintenance and reconciliation routes |
| `X-Admin-Seed-Key` header | Election Authorization admin eligibility seed route |

## 3. Health and Diagnostics

| Component | Method + Route | Auth |
| --- | --- | --- |
| CynetAuth | `GET /health` | None |
| Election Authorization | `GET /health` | None |
| Election Processing | `GET /health` | None |
| Transparency API | `GET /api/transparency/health` | None |
| Watcher API | `GET /api/watcher/health` | None |
| Threshold Decryption API | `GET /api/decryption/health` | None |

## 4. Core Endpoints by Component

### 4.1 CynetAuth (`/api/auth`)

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Register demo user | None |
| POST | `/api/auth/login` | Acquire access token | None |
| GET | `/api/auth/me` | Resolve current principal | Bearer |

### 4.2 Election Authorization (`/api/authorization`)

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/authorization/profile` | Get or create subject VID profile | Bearer |
| GET | `/api/authorization/elections/{electionId}/state` | Check eligibility/consumption state | Bearer |
| POST | `/api/authorization/elections/{electionId}/grant` | Issue one-time grant | Bearer |
| POST | `/api/authorization/elections/{electionId}/consume` | Atomically consume grant | Bearer |
| POST | `/api/authorization/admin/elections/{electionId}/eligibility` | Seed eligibility for local runs | `X-Admin-Seed-Key` |

### 4.3 Voter Outbox Operator API (`/api/voter/outbox`)

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/voter/outbox/state` | View pending durable outbox entries and policy stats | Authorized session |
| POST | `/api/voter/outbox/retry-next` | Retry next failed queue publication | Authorized session |
| POST | `/api/voter/outbox/compact` | Purge stale/excess entries | Authorized session |
| DELETE | `/api/voter/outbox/elections/{electionId}/nullifiers/{ballotNullifier}` | Remove targeted pending entry | Authorized session |

### 4.4 Election Processing (`/api/processing`)

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/processing/status` | Coordinator status | None |
| GET | `/api/processing/elections/{electionId}/state` | Per-election state | None |
| GET | `/api/processing/prover/readiness` | Prover readiness check | None |
| POST | `/api/processing/elections/{electionId}/pause` | Pause election processing | `X-Processing-Auth` (`processing.operator`) |
| POST | `/api/processing/elections/{electionId}/resume` | Resume election processing | `X-Processing-Auth` (`processing.operator`) |
| POST | `/api/processing/elections/{electionId}/retry-failed-batch` | Retry blocked failed batch (re-verifies proof; never settles on failure) | `X-Processing-Auth` (`processing.operator`) |
| POST | `/api/processing/ingest` | Ingest ballot submission package (validated synchronously; 400 on malformed/duplicate input) | `X-Processing-Auth` (`processing.ingest`) |

### 4.5 Transparency API (`/api/transparency`)

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| GET | `/api/transparency/elections/{electionId}/snapshot` | Get verified result snapshot (fails closed on stale/canonical-mismatched projections) | None |
| GET | `/api/transparency/elections/{electionId}/verify/{vid}` | VID inclusion/consistency verification (fails closed on stale/canonical-mismatched projections) | None |
| GET | `/api/transparency/elections/{electionId}/projection-status` | Get projection status and consistency digests | None |
| GET | `/api/transparency/elections/{electionId}/canonical-state` | Read canonical settlement view | None |
| POST | `/api/transparency/elections/{electionId}/ingest` | Ingest projection evidence | `X-Transparency-Auth` (`transparency.ingestion`) |
| POST | `/api/transparency/elections/{electionId}/mark-stale` | Mark projection stale | `X-Transparency-Auth` (`transparency.ingestion`) |
| POST | `/api/transparency/elections/{electionId}/reindex` | Reindex election projection | `X-Transparency-Auth` (`transparency.ingestion`) |
| POST | `/api/transparency/events/resync` | Queue canonical resync trigger | `X-Transparency-Auth` (`transparency.ingestion`) |
| POST | `/api/transparency/reconciliation/disputes` | Open reconciliation dispute | `X-Transparency-Auth` (`transparency.reconciliation`) |
| POST | `/api/transparency/reconciliation/{caseId}/override` | Request reconciliation override | `X-Transparency-Auth` (`transparency.reconciliation`) |
| POST | `/api/transparency/reconciliation/{caseId}/resolve` | Resolve reconciliation case | `X-Transparency-Auth` (`transparency.reconciliation`) |
| GET | `/api/transparency/elections/{electionId}/reconciliation/cases` | List reconciliation cases | None |

### 4.6 Watcher API (`/api/watcher`)

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| POST | `/api/watcher/elections/{electionId}/run-audit` | Execute watcher audit and create signed report | `X-Operator-Key` |
| GET | `/api/watcher/elections/{electionId}/latest-report` | Get latest audit report | `X-Operator-Key` |
| GET | `/api/watcher/elections/{electionId}/reports` | List all reports by election | `X-Operator-Key` |
| GET | `/api/watcher/reports/{reportId}` | Get report by id | `X-Operator-Key` |
| GET | `/api/watcher/reports/{reportId}/evidence` | Export report evidence payload | `X-Operator-Key` |
| GET | `/api/watcher/operator/elections/{electionId}/summary` | Operator summary aggregate | `X-Operator-Key` |

### 4.7 Threshold Decryption API (`/api/decryption`)

| Method | Route | Purpose | Auth |
| --- | --- | --- | --- |
| POST | `/api/decryption/elections/{electionId}/ceremony` | Propose ceremony | `X-Operator-Key` |
| POST | `/api/decryption/elections/{electionId}/ceremony/approve` | Add governance approval | `X-Operator-Key` |
| POST | `/api/decryption/elections/{electionId}/ceremony/share` | Submit trustee share digest | `X-Operator-Key` |
| POST | `/api/decryption/elections/{electionId}/ceremony/finalize` | Finalize with candidate tallies | `X-Operator-Key` |
| POST | `/api/decryption/elections/{electionId}/publish-final-tally` | Publish tally artifact reference | `X-Operator-Key` |
| GET | `/api/decryption/elections/{electionId}/ceremony` | Get ceremony by election | `X-Operator-Key` |
| GET | `/api/decryption/ceremonies/{ceremonyId}` | Get ceremony by id | `X-Operator-Key` |

## 5. Web UI Surfaces

| Component | Primary routes |
| --- | --- |
| Voter Component | `/`, `/Login`, `/Dashboard` |
| Result Portal | `/`, `/Results`, `/Verify` |

## 6. OpenAPI Endpoints in Development

In Development mode, the following APIs expose OpenAPI endpoints:
- CynetAuth
- Election Authorization
- Transparency API
- Watcher API
- Threshold Decryption API

See each component `Program.cs` for `MapOpenApi()` behavior.
