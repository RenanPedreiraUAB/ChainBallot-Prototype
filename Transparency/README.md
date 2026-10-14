# Transparency / Audit API

This component provides the read-model boundary for vote verification and result snapshot queries.

Current scope:

- Serve verified election snapshots for downstream result publication.
- Serve VID verification outcomes with settlement and inclusion references.
- Keep contracts aligned with `ChainBallot.PublicContracts`.

## Projects

- `TransparencyAudit.Api` - ASP.NET Core Web API host.
- `TransparencyAudit.Api.Tests` - integration tests for endpoint behavior.

## API Endpoints

Base path: `/api/transparency`

- `GET /health`
  - Basic service health check.
- `GET /elections/{electionId}/snapshot`
  - Returns `VerifiedResultSnapshot` when projection exists.
  - Returns `404` when election projection does not exist.
- `GET /elections/{electionId}/verify/{vid}`
  - Returns `VidVerificationResponse` with outcome:
    - `Verified` (only when the projection is fresh and, where a canonical read source is configured, corroborated by canonical settlement state)
    - `NotFound`
    - `ElectionNotFinalized`
    - `Inconsistent` (projection marked stale, or a configured canonical read source contradicts the projection's Finalized state)
- `GET /elections/{electionId}/projection-status`
  - Returns projection operational state (`isStale`, `staleReason`, `consistencyDigest`, evidence counts).
- `GET /elections/{electionId}/canonical-state`
  - Returns canonical election finalization state from configured Solana read adapter mode.
- `POST /elections/{electionId}/ingest` — requires `X-Transparency-Auth` (role `transparency.ingestion`)
  - Ingests processing evidence into the local projection store.
  - Computes deterministic consistency digests for VID entries and election projection.
- `POST /elections/{electionId}/mark-stale` — requires `X-Transparency-Auth` (role `transparency.ingestion`)
  - Marks projection stale with an explicit reason for operational recovery workflows.
- `POST /elections/{electionId}/reindex` — requires `X-Transparency-Auth` (role `transparency.ingestion`)
  - Recomputes consistency digests and clears stale state after successful reindex.
- `POST /events/resync` — requires `X-Transparency-Auth` (role `transparency.ingestion`)
  - Queues a source-driven canonical resync event for asynchronous reindex handling.
- `POST /reconciliation/disputes` — requires `X-Transparency-Auth` (role `transparency.reconciliation`)
  - Opens reconciliation dispute case and records immutable case-event trail.
- `POST /reconciliation/{caseId}/override` — requires `X-Transparency-Auth` (role `transparency.reconciliation`)
  - Records override request/decision event and transitions case state.
- `POST /reconciliation/{caseId}/resolve` — requires `X-Transparency-Auth` (role `transparency.reconciliation`)
  - Resolves reconciliation case and records immutable resolution event.
- `GET /elections/{electionId}/reconciliation/cases`
  - Lists reconciliation cases and their immutable case-event history.

## Authentication and Authorization

Privileged routes (evidence ingestion, mark-stale, reindex, resync, and reconciliation dispute/override/resolve)
require a header-token credential bound to a role, configured under `Transparency:Authentication`:

- `HeaderName` (default `X-Transparency-Auth`)
- `Credentials`: a list of `{ Token, Name, Roles }` entries. Roles in use: `transparency.ingestion`, `transparency.reconciliation`.

Read-only routes (snapshot, verify, projection-status, canonical-state, list reconciliation cases) remain
unauthenticated. This is a prototype-grade shared-secret mechanism, not a production identity system.

## Projection Storage

Default storage is a file-backed projection snapshot JSON.

Resolution order:

- `Transparency:ProjectionStorePath` from configuration (or `CHAINBALLOT_Transparency__ProjectionStorePath` environment override).
- Default fallback path: `%LocalAppData%/ChainBallot/Transparency/projections.json`.

If the projection file does not exist, the service creates an empty snapshot file.

The current read model now supports non-infra operational workflows:

- Evidence ingestion from processing output contracts.
- Projection stale marking.
- Reindex digest recomputation and stale-state recovery.
- Metadata and audit digest correlation during ingest/reindex.
- Automatic stale marking when authoritative digest correlation fails.

## Correlation Model (Current)

- The component exposes correlation abstractions for metadata and audit evidence digests.
- HTTP authoritative digest source is the runtime default implementation.
- On mismatch (or missing provided digest when authoritative digest exists), projection is marked stale with explicit reason.
- Effective digests are normalized to authoritative values before consistency digest recomputation.

This provides deterministic non-infra reconciliation behavior in local runtime using metadata-hosted HTTP digest endpoints.

## Authoritative Digest Source Modes

- `Http` (default runtime): resolves authoritative digests from configured HTTP endpoint templates.
- `InMemory` (fallback/test): resolves authoritative digests from configuration maps.

Relevant settings under `Transparency`:

- `AuthoritativeDigestSourceMode`
- `MetadataDigestEndpointTemplate` (supports `{electionId}` token)
- `AuditDigestEndpointTemplate` (supports `{electionId}` token)
- `AuthoritativeHttpTimeoutSeconds`
- `AuthoritativeMetadataDigests`
- `AuthoritativeAuditDigests`

## Automated Reindex Orchestration

The API now includes a background worker that periodically reindexes configured elections.

Relevant settings under `Transparency`:

- `AutoReindexEnabled`
- `AutoReindexIntervalSeconds`
- `AutoReindexElectionIds`

Current behavior:

- Reindex is interval-driven and configuration-scoped.
- Missing election projections are logged and skipped.
- Canonical-source event-driven triggers remain a follow-up implementation.

## Canonical Resync Trigger Path

- An in-memory canonical resync event source is implemented.
- `POST /events/resync` publishes reindex trigger events.
- A background consumer processes events and invokes projection reindex.

## Canonical Solana Read Modes

- `ProjectionOnly` (default): no external canonical read performed.
- `Mock`: resolves canonical finalized-state from configured map.
- `Http`: resolves canonical finalized-state from configured endpoint template.

Relevant settings:

- `SolanaReadMode`
- `SolanaElectionStateEndpointTemplate`
- `SolanaHttpTimeoutSeconds`
- `MockSolanaFinalizedStates`

## Run

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Transparency\TransparencyAudit.Api\TransparencyAudit.Api.csproj --no-restore
```

## Test

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Transparency\TransparencyAudit.sln --no-restore
```
