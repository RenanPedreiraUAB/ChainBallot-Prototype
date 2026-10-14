# Manual End-to-End Testing Guide

This is a hands-on, click-through walkthrough for manually exercising the full election lifecycle yourself: configure an election, cast votes as different voters, view the published results, and check the transparency/watcher audit trail. It complements the automated [`docker/e2e-smoke.ps1`](../docker/e2e-smoke.ps1) script referenced in the root [README's Ordered Quickstart](../README.md#ordered-quickstart) — use this guide when you want to drive the real UI yourself instead of running a scripted smoke test.

## Prerequisites

- Docker Desktop running
- .NET 9 SDK installed (to run the Configuration desktop app locally)
- PowerShell

## Step 0 — Start the local stack

From the repository root:

```powershell
docker compose -f docker-compose.local.yml up -d
```

Confirm every service is healthy:

```powershell
@(
  'http://localhost:5015/health',
  'http://localhost:5147/health',
  'http://localhost:5231/',
  'http://localhost:5301/health',
  'http://localhost:5264/api/transparency/health',
  'http://localhost:5181/',
  'http://localhost:5161/api/watcher/health',
  'http://localhost:5287/api/decryption/health',
  'http://localhost:8088/health'
) | ForEach-Object { try { $r = Invoke-WebRequest -Uri $_ -UseBasicParsing -TimeoutSec 10; "$_ -> $($r.StatusCode)" } catch { "$_ -> FAILED: $($_.Exception.Message)" } }
```

Every line should end in `-> 200`.

Seed demo voter accounts (idempotent — safe to rerun):

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\seeding\seed-users.ps1 -UserCount 10 -SeedEligibility
```

This creates/reuses `voter001` … `voter010` with passwords `ChainBallot!2026-001` … `ChainBallot!2026-010` (see [`docker/seeding/cynetauth-users.csv`](../docker/seeding/cynetauth-users.csv)) and seeds their eligibility for the default `election-demo` election.

## Step 1 — Configure an election

Run the Election Configuration Component (Avalonia desktop app) locally:

```powershell
dotnet run --project .\Configuration\ConfigurationComponent\ConfigurationComponent.csproj
```

In the app:

1. Click **Create Election**.
2. On tab **1. Election Details**: set an election name, an **Opening Date** of tomorrow (must be strictly in the future), and a later **Closing Date**. Leave Network, Program ID, and Metadata Endpoint at their defaults.
3. Click **Next: Add Candidates →**.
4. On tab **2. Candidates**: add at least two candidates (name + party). Note the numeric **ID** shown in the grid for each candidate — you'll need these later to tally results.
5. Go to tab **3. Deploy**: optionally click **Generate Recommendation**, then click **Deploy Election**.
6. Deployment succeeds using a local preflight signature (`rpc_preflight_...`) — this is expected for local testing without a real Solana payer key.
7. Copy the **election ID** from the "Metadata URI" line in the **Deployment Result** box. It looks like `/elections/<GUID>.json` — the GUID is your election ID.

Verify the election published correctly:

```powershell
Invoke-RestMethod http://localhost:8088/elections/<ELECTION_ID>.json
```

You should see your candidates and a metadata hash.

Seed eligibility for this new election (reuses the same 10 demo accounts):

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\seeding\seed-users.ps1 -ElectionId <ELECTION_ID> -UserCount 10 -SeedEligibility
```

## Step 2 — Vote

1. Open `http://localhost:5231/Login` in your browser.
2. Sign in as `voter001` / `ChainBallot!2026-001`.
3. In the **Election ID** box, paste `<ELECTION_ID>` and click **Load election**.
4. Select a candidate from the dropdown and click **Encrypt and queue ballot**.
5. Repeat with a different account (`voter002` / `ChainBallot!2026-002`, etc.) in a private/incognito window so you can vote for more than one candidate and see a real distribution in the results.

Wait about 20–30 seconds for the ballot batch to close and anchor, then confirm:

```powershell
Invoke-RestMethod "http://localhost:5301/api/processing/elections/<ELECTION_ID>/state"
```

`anchoredBallotCount` should equal the number of votes you cast, with `pendingBallotCount: 0`.

## Step 3 — See the results

Because ballots are encrypted end-to-end in this prototype, the final tally is supplied by the threshold-decryption ceremony rather than derived automatically. Replace `<ELECTION_ID>` and the candidate vote counts (using the numeric candidate IDs from Step 1) below:

```powershell
$id = "<ELECTION_ID>"; $h = @{ "X-Operator-Key" = "decryption-operator-key" }
$transparencyIngestHeaders = @{ "X-Transparency-Auth" = "transparency-ingestion-dev-token" }

Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$id/ceremony" -Headers $h -ContentType application/json -Body (@{requiredShares=3;requiredApprovals=2;actor="manual-test"} | ConvertTo-Json)

foreach ($a in @("governor-a","governor-b")) {
  Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$id/ceremony/approve" -Headers $h -ContentType application/json -Body (@{approverId=$a;note="manual"} | ConvertTo-Json) | Out-Null
}
foreach ($t in @("trustee-a","trustee-b","trustee-c")) {
  Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$id/ceremony/share" -Headers $h -ContentType application/json -Body (@{trusteeId=$t;shareDigest="digest-$t"} | ConvertTo-Json) | Out-Null
}

$tallies = @{ "1" = <CANDIDATE_1_VOTES>; "2" = <CANDIDATE_2_VOTES> }
$final = Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$id/ceremony/finalize" -Headers $h -ContentType application/json -Body (@{actor="manual-test";tallies=$tallies} | ConvertTo-Json)
$pub = Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$id/publish-final-tally" -Headers $h -ContentType application/json -Body (@{actor="manual-test"} | ConvertTo-Json)

# metadataDigest is the "metadataHash" from the election's metadata JSON (Step 1) — required or the
# projection will be flagged stale and the Step 4 Watcher audit will report "Inconsistent".
$metadataDigest = (Invoke-RestMethod "http://localhost:8088/elections/$id.json").verification.metadataHash
$snap = Invoke-RestMethod "http://localhost:5264/api/transparency/elections/$id/snapshot"
Invoke-RestMethod -Method Post -Uri "http://localhost:5264/api/transparency/elections/$id/ingest" -Headers $transparencyIngestHeaders -ContentType application/json -Body (@{
  lifecycleState="Finalized"; resultDigest=$pub.resultDigest; settlementReference=$snap.settlementReference
  candidateTallies=$tallies; vidEntries=@(); metadataDigest=$metadataDigest; isStale=$false
} | ConvertTo-Json -Depth 6)
```

Then open:

```
http://localhost:5181/Results?ElectionId=<ELECTION_ID>
```

You should see the finalized tallies you supplied.

## Step 4 — Transparency and Watcher

**Transparency API** (no auth required):

```powershell
Invoke-RestMethod "http://localhost:5264/api/transparency/elections/<ELECTION_ID>/snapshot"
Invoke-RestMethod "http://localhost:5264/api/transparency/elections/<ELECTION_ID>/projection-status"
Invoke-RestMethod "http://localhost:5264/api/transparency/elections/<ELECTION_ID>/canonical-state"
```

VID verification (grab a VID from the Voter dashboard after logging in — it's shown near "Eligibility"):

```powershell
Invoke-RestMethod "http://localhost:5264/api/transparency/elections/<ELECTION_ID>/verify/<VID>"
```

> **Note:** the `snapshot` response never contains VID entries (it's tally/settlement data only). Because this
> guide's Step 3 ingest doesn't have access to per-ballot Merkle proof data (the manual UI flow never surfaces
> `ballotCommitment`/`batchId` to the voter), a manual `ingest` call with empty `vidEntries` will make VID
> verification return `NotFound` for every voter, even though their ballots anchored correctly. To actually see a
> `Verified` result in this manual flow, you must re-run the ingest with reconstructed `vidEntries` (one entry per
> voter, using their VID from the dashboard); exact inclusion-proof fields can reuse the last anchored batch's
> `batchRoot`/`proofReference` from the Step 2 `/state` response, since this is for local demonstration only. For a
> fully accurate, cryptographically real VID verification pass (proof data tracked correctly per ballot), use
> [`docker/e2e-smoke.ps1 -FullCycle`](../docker/e2e-smoke.ps1) instead, which does this automatically.

**Watcher API** (requires the operator key):

```powershell
$w = @{ "X-Operator-Key" = "watcher-operator-key" }
Invoke-RestMethod -Method Post -Uri "http://localhost:5161/api/watcher/elections/<ELECTION_ID>/run-audit" -Headers $w -ContentType application/json -Body (@{requestedBy="manual-test"} | ConvertTo-Json)
Invoke-RestMethod "http://localhost:5161/api/watcher/elections/<ELECTION_ID>/latest-report" -Headers $w
Invoke-RestMethod "http://localhost:5161/api/watcher/operator/elections/<ELECTION_ID>/summary" -Headers $w
```

The audit report `status` should read `Consistent` (with `metadataDigest` correctly supplied in Step 3, `isStale` will be `false`).

The report shows per-check consistency status (metadata digest, audit digest, canonical state, projection) and a signed evidence export via `GET /api/watcher/reports/{reportId}/evidence`.

## Troubleshooting

- **Docker Desktop feels wedged** (very slow container start/stop, or `docker` commands hang): restart Docker Desktop, or restart your machine, then rerun Step 0.
- **A ballot's batch fails to anchor** with a Solana connection error: the local `solana-validator` container may still be warming up (or its ledger volume has grown stale after repeated restarts). Give it another 20–30 seconds, or reset its ledger:
  ```powershell
  docker compose -f docker-compose.local.yml stop solana-validator
  docker compose -f docker-compose.local.yml rm -f solana-validator
  docker volume rm chainballot_solana-ledger
  docker compose -f docker-compose.local.yml up -d solana-validator
  ```
- **Need a clean slate**: `docker compose -f docker-compose.local.yml down -v` removes all containers and volumes; rerun from Step 0.

See also: [docs/API-Reference.md](API-Reference.md) for the full route index, and [Prototype.md](../Prototype.md) for what each component actually implements versus the ideal framework in [Architecture.md](../Architecture.md).
