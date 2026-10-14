# Election Configuration Component

This component implements the Election Configuration Component role from `Architecture.md` §3.1: the offline/administrative provisioning tool used to define, validate, and deploy an election before any voter-facing operation can occur.

Responsibilities:

- Capture and edit an election draft (name, schedule, candidates, allocation/visibility policy, cryptographic parameters, expected participation).
- Validate election rules (candidate uniqueness, schedule ordering, lifecycle-parameter consistency) before any artifact is produced.
- Generate canonical election metadata and compute its SHA-256 hash.
- Derive scale recommendations (consumer/partition/batch-size guidance) from expected voter volume, duration, and peak-participation assumptions.
- Initialize the reusable election program on the canonical settlement ledger (Mock or Solana-RPC mode) and record the returned contract reference.
- Persist and publish the deployment manifest that binds policy, metadata location, and settlement context for downstream components.

## Internal Structure Pattern

- `Application/` - dependency registration and startup wiring.
- `Domain/` - election draft, metadata, and deployment manifest models.
- `Infrastructure/` - file-based repositories and HTTP metadata upload client.
- `Models/` - view-facing data shapes.
- `Repositories/` - persistence abstractions for drafts and deployment artifacts.
- `Services/` - validation, metadata/hash generation, scale recommendation, and Solana (Mock/RPC) deployment services.
- `Blockchain/` - Solana service boundary implementations.
- `Utilities/` - shared helpers.
- `ViewModels/` / `Views/` - Avalonia MVVM presentation layer.
- `Resources/` / `Assets/` - desktop UI resources.

## Behavior Notes

- Only a `Draft` election can be mutated; an invalid draft can never reach deployment.
- Metadata is published to the Election Metadata Repository before ledger initialization, so a partial failure never leaves an unpublished-but-deployed election.
- Deployment is idempotent: retrying after metadata publication or ledger initialization succeeds does not republish metadata or re-invoke the ledger program.

## Configuration

Runtime configuration loads from `appsettings.json` plus `CHAINBALLOT_` environment variable overrides. See [`docs/Configuration-Reference.md`](../docs/Configuration-Reference.md) for the full variable reference.

## Run

Start the local Docker stack first so the metadata service and Solana validator are available. Then, from the repository root:

```powershell
dotnet run --project .\Configuration\ConfigurationComponent\ConfigurationComponent.csproj --no-restore
```

For local integration, keep the Metadata Endpoint set to `http://localhost:8088/`. A successful deployment publishes an election document that the Voter Component can load from the same local service.

## Test

From repository root:

```powershell
dotnet test .\Configuration\ConfigurationComponent.sln --no-restore
```
