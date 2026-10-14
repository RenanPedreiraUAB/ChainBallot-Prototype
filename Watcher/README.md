# Watcher / Audit Component

This component provides operational supervision and independent audit reporting across transparency projections, authoritative digest sources, and canonical settlement state.

## Projects

- `WatcherAudit.Api` - ASP.NET Core Web API host.
- `WatcherAudit.Api.Tests` - unit and integration tests.

## Implemented Scope

- Audit execution endpoint per election.
- Cross-checks across:
  - Transparency snapshot,
  - Transparency projection status,
  - Transparency canonical state,
  - Authoritative metadata/audit digest source.
- Inconsistency detection with structured check outcomes.
- File-backed report persistence with latest/list/get queries.
- Operator summary endpoint for election-level audit status.
- Evidence export endpoint containing checks, evidence payload, report digest, and signature.
- Mode-based authoritative digest source (`Http` runtime default, `InMemory` fallback/test).
- Operator access control on non-health endpoints via configurable header key.

## Remaining (Non-Code/Infra-Later)

- Live canonical provider credentials and production endpoint rollout.
- Production signing key governance for report signatures.
- Rich operator dashboard UI (current operator view is API-based).
- Full live-host end-to-end validation against deployed transparency + metadata/audit providers.

## API Surface

- `GET /api/watcher/health`
- `POST /api/watcher/elections/{electionId}/run-audit`
- `GET /api/watcher/elections/{electionId}/latest-report`
- `GET /api/watcher/elections/{electionId}/reports`
- `GET /api/watcher/reports/{reportId}`
- `GET /api/watcher/reports/{reportId}/evidence`
- `GET /api/watcher/operator/elections/{electionId}/summary`

## Configuration

`Watcher` section:

- `TransparencyBaseUrl`
- `TransparencyTimeoutSeconds`
- `DigestSourceMode` (`Http` runtime default, or `InMemory` fallback/test)
- `DigestHttpTimeoutSeconds`
- `MetadataDigestEndpointTemplate`
- `AuditDigestEndpointTemplate`
- `MetadataDigests`
- `AuditDigests`
- `ReportStorePath`
- `ReportSigningKey`
- `OperatorAccessHeaderName`
- `OperatorAccessKey`

Environment override prefix is `CHAINBALLOT_`.

## Run

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Watcher\WatcherAudit.Api\WatcherAudit.Api.csproj --no-restore
```

## Test

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Watcher\WatcherAudit.sln --no-restore
```
