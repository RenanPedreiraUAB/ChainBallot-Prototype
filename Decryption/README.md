# Threshold Decryption Orchestration

This component coordinates post-close threshold decryption ceremony flow and final tally publication artifacts.

## Projects

- `ThresholdDecryption.Api` - ASP.NET Core Web API host.
- `ThresholdDecryption.Api.Tests` - unit and integration tests.

## Implemented Scope

- Ceremony proposal endpoint per election, gated on a canonical election Closed/Finalized precondition (see below).
- Governance approval workflow with configurable approvers and approval threshold.
- Trustee key-share submission workflow with configurable trustee list and share threshold.
- Decryption finalization workflow that re-checks the canonical precondition and computes a deterministic final result digest.
- Final tally publication workflow that emits a publication reference artifact.
- File-backed ceremony persistence that survives process restarts.
- Ceremony retrieval endpoints by election and by ceremony id.
- Operator access control on non-health endpoints via configurable header key.
- Proposal/finalization/publication are idempotent: repeated calls return the existing ceremony/digest/reference rather than creating a duplicate logical result.

## Canonical Closed/Finalized Precondition

Before a ceremony may be proposed, and again before finalization, the component calls Transparency's public
`GET /api/transparency/elections/{electionId}/canonical-state` endpoint and requires `isFinalized == true`.
If the endpoint is unreachable, returns a non-success status, returns a malformed body, or reports
`isFinalized == false`, the operation is rejected (fails closed) and a `closure-check-rejected` event is
recorded; the ceremony is never advanced.

**Prototype simplification:** this is the only network-reachable canonical lifecycle signal exposed by any
component in this system today. There is no distinct "Closed-but-not-yet-Finalized" signal anywhere
reachable over HTTP (the Configuration component's richer Draft/Deployed/Open/Closed lifecycle exists only
in-process in a desktop application). `IsFinalized` is therefore used as the Closed-state gate; it is not
renamed or reinterpreted as a general lifecycle enum.

Configuration: `ThresholdDecryption:TransparencyBaseUrl` (see Configuration section below).

## API Surface

- `GET /api/decryption/health`
- `POST /api/decryption/elections/{electionId}/ceremony`
- `POST /api/decryption/elections/{electionId}/ceremony/approve`
- `POST /api/decryption/elections/{electionId}/ceremony/share`
- `POST /api/decryption/elections/{electionId}/ceremony/finalize`
- `POST /api/decryption/elections/{electionId}/publish-final-tally`
- `GET /api/decryption/elections/{electionId}/ceremony`
- `GET /api/decryption/ceremonies/{ceremonyId}`

## Configuration

`ThresholdDecryption` section:

- `DefaultRequiredShares`
- `DefaultRequiredApprovals`
- `AllowedTrustees`
- `GovernanceApprovers`
- `StorePath`
- `OperatorAccessHeaderName`
- `OperatorAccessKey`
- `TransparencyBaseUrl` (base URL used to read the canonical election Closed/Finalized precondition)

Environment override prefix is `CHAINBALLOT_`.

## Run

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Decryption\ThresholdDecryption.Api\ThresholdDecryption.Api.csproj --no-restore
```

## Test

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Decryption\ThresholdDecryption.sln --no-restore
```
