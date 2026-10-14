# ElectionProcessing Component

This component implements the outbound election processing service for Chain Ballot.

Current implementation scope:

- ASP.NET Core Web API host (`ElectionProcessing.Api`)
- Background worker that runs processing cycles
- Queue abstraction with selectable in-memory and Kafka adapters
- Batch closure by count and max wait
- Strategy abstractions for Merkle tree and proof generation
- Default binary SHA-256 Merkle strategy
- Default deterministic stub prover strategy (replaceable)
- Optional local process prover strategy and external HTTP prover strategy
- Proof verification hook runs before settlement anchoring
- Settlement anchor abstraction with selectable mock and Solana RPC implementations
- Batch validation for duplicate commitments/nullifiers and replay against previously seen state
- Per-election tally counters and state progression tracking
- Per-election blocking on failed batch processing
- Admin endpoints for status, election state, pause/resume, failed-batch retry, and prover readiness

## Run

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\ElectionProcessing\ElectionProcessing\ElectionProcessing.Api\ElectionProcessing.Api.csproj --no-restore
```

## Test

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\ElectionProcessing\ElectionProcessing\ElectionProcessing.sln --no-restore
```

## Configuration

`ElectionProcessing.Api/appsettings.json`

- `Processing:MaxBallotsPerBatch` (default: `32`)
- `Processing:MaxBatchWaitSeconds` (default: `20`)
- `Processing:PollIntervalMilliseconds` (default: `1000`)
- `Processing:QueueMode` (`Kafka` default runtime, or `InMemory` fallback/test)
- `Processing:Kafka:BootstrapServers` (required when `QueueMode=Kafka`)
- `Processing:Kafka:Topic` (required when `QueueMode=Kafka`)
- `Processing:Kafka:ConsumerGroupId` (default: `chainballot-outbound`)
- `Processing:Kafka:PollBatchSize` (default: `64`)
- `Processing:Kafka:ConsumeTimeoutMilliseconds` (default: `100`)
- `Processing:ProverMode` (`DeterministicStub` default, `ExternalHttp`, or `LocalProcess`)
- `Processing:ExternalProver:Endpoint` (required when `ProverMode=ExternalHttp`)
- `Processing:ExternalProver:ApiKey` (optional bearer token)
- `Processing:ExternalProver:TimeoutSeconds` (default: `30`)
- `Processing:LocalProcessProver:ExecutablePath` (required when `ProverMode=LocalProcess`)
- `Processing:LocalProcessProver:Arguments` (optional command arguments)
- `Processing:LocalProcessProver:WorkingDirectory` (optional process working directory)
- `Processing:LocalProcessProver:TimeoutSeconds` (default: `60`)
- `Processing:SettlementMode` (`SolanaRpc` default runtime, or `Mock` fallback/test)
- `Processing:SolanaRpc:RpcEndpoint` (required when `SettlementMode=SolanaRpc`)
- `Processing:SolanaRpc:AnchorSubmitMethod` (default: `chainballot_anchorBatch`)
- `Processing:SolanaRpc:RpcApiKey` (optional bearer token)
- `Processing:SolanaRpc:TimeoutSeconds` (default: `30`)
- `Processing:SettlementNetwork`
- `Processing:SettlementProgramId`
- `Processing:SettlementCommitment`
- `Processing:ProjectionPublishing:Enabled`
- `Processing:ProjectionPublishing:BaseUrl`
- `Processing:ProjectionPublishing:TimeoutSeconds`
- `Processing:ProjectionPublishing:LifecycleState`
