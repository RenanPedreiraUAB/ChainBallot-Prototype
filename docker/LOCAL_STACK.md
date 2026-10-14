# Local Docker Stack

This stack boots the web/API components for local prototype validation with minimal external dependencies.

## Included Services

- CynetAuth (`5015`)
- Election Authorization (`5147`)
- Voter Component (`5231`)
- Election Processing (`5301`)
- Transparency API (`5264`)
- Result Portal (`5181`)
- Watcher API (`5161`)
- Threshold Decryption API (`5287`)
- Redpanda Kafka-compatible broker (`9092`)
- Writable local metadata service (`8088`)
- Solana local validator (`8899`)

## Start

```powershell
docker compose -f docker-compose.local.yml up --build -d
```

## Stop

```powershell
docker compose -f docker-compose.local.yml down
```

To also remove volumes:

```powershell
docker compose -f docker-compose.local.yml down -v
```

## Seed Demo Users (200)

After the stack is running, seed local CynetAuth users and optional election eligibility:

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\seeding\seed-users.ps1 -UserCount 200 -SeedEligibility
```

Generated credentials are stored at:

- `docker/seeding/cynetauth-users.csv`

## One-Command E2E Smoke Test

Run an integrated smoke flow (boot stack, seed users, submit ballots, close the election, publish the tally, verify processing/transparency/watcher):

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\e2e-smoke.ps1
```

If the stack is already running and already seeded:

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\e2e-smoke.ps1 -SkipDockerUp -SkipSeeding
```

## Notes

- Outbound is configured for Kafka mode against Redpanda.
- Inbound is configured for Kafka mode and publishes directly to Redpanda topic `chainballot-ballots`.
- Outbound consumes the same topic in Kafka mode, making Kafka the canonical local prototype queue transport.
- Outbound automatically publishes anchored-batch evidence into Transparency ingest endpoints.
- Transparency and Watcher authoritative digest correlation run in HTTP mode against metadata-hosted digest endpoints.
- Outbound settlement anchoring runs in `SolanaRpc` mode against local `solana-test-validator`.
- Transparency canonical-state reads run in `Http` mode against local `solana-test-validator`.
- Watcher and Decryption non-health endpoints require operator header keys:
  - `X-Operator-Key: watcher-operator-key`
  - `X-Operator-Key: decryption-operator-key`
- Decryption ceremony proposal/finalisation additionally requires Transparency's canonical-state endpoint to report `isFinalized: true` for the target election (fails closed if unreachable/false/malformed).
- Election Processing's pause/resume/retry-failed-batch/ingest routes require a role-bound header token:
  - `X-Processing-Auth: processing-operator-dev-token` (role `processing.operator`, for pause/resume/retry-failed-batch)
  - `X-Processing-Auth: processing-ingest-dev-token` (role `processing.ingest`, for direct ingest)
- Transparency's ingestion/maintenance and reconciliation endpoints require a role-bound header token:
  - `X-Transparency-Auth: transparency-ingestion-dev-token` (role `transparency.ingestion`, for ingest/mark-stale/reindex/resync)
  - `X-Transparency-Auth: transparency-reconciliation-dev-token` (role `transparency.reconciliation`, for dispute/override/resolve)

