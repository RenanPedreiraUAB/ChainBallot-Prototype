# Chain Ballot Configuration Reference (Local Prototype)

This document centralizes key runtime configuration used by the local Docker prototype.

Primary source of truth: `docker-compose.local.yml`.

## 1. Global Runtime Defaults

| Variable | Example value | Purpose |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:5301` | HTTP bind address inside container |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Environment profile |

## 2. Security-Sensitive Variables (Must Be Replaced Outside Demo)

| Variable | Component | Purpose |
| --- | --- | --- |
| `CHAINBALLOT_CYNETAUTH_JWT_SIGNING_KEY` | CynetAuth | Signs JWT tokens |
| `CHAINBALLOT_AUTHZ_TRUSTEDAUTH_SIGNING_KEY` | Authorization | Validates trusted JWT signatures |
| `CHAINBALLOT_VOTER__BALLOTENCRYPTIONKEY` | Voter | AES key used for ballot encryption |
| `CHAINBALLOT_WATCHER__REPORTSIGNINGKEY` | Watcher | Signs watcher report digest evidence |
| `CHAINBALLOT_WATCHER__OPERATORACCESSKEY` | Watcher | Protects watcher operator routes |
| `CHAINBALLOT_THRESHOLDDECRYPTION__OPERATORACCESSKEY` | Decryption | Protects decryption operator routes |
| `Processing__Authentication__Credentials__N__Token` | Election Processing | Protects pause/resume/retry-failed-batch/ingest routes (role-bound) |
| `Processing__ProjectionPublishing__AuthToken` | Election Processing | Outbound-side credential sent to Transparency's ingest endpoint |
| `CHAINBALLOT_TRANSPARENCY__AUTHENTICATION__CREDENTIALS__N__TOKEN` | Transparency | Protects ingestion/maintenance/reconciliation routes (role-bound) |
| `CHAINBALLOT_RESULTPORTAL__HEADERAUTHTOKEN` | Result Portal | Expected token value for `HeaderToken` visibility mode |
| `CHAINBALLOT_RESULTPORTAL__OIDCCLIENTSECRET` | Result Portal | OIDC client secret for `Oidc` visibility mode |
| `Admin__SeedKey` | Authorization | Protects admin eligibility seed route |

## 3. Per-Component Runtime Settings

### 3.1 CynetAuth

| Variable | Example | Purpose |
| --- | --- | --- |
| `ConnectionStrings__AuthDb` | `Data Source=/data/cynetauth.db` | SQLite file path |
| `CHAINBALLOT_CYNETAUTH_JWT_SIGNING_KEY` | `CHANGE_ME_...` | JWT signing key |
| `Jwt__Issuer` | `cynetauth` | JWT issuer claim |
| `Jwt__Audience` | `chainballot` | JWT audience claim |
| `Jwt__AccessTokenMinutes` | `30` | Access token lifetime in minutes |

### 3.2 Election Authorization

| Variable | Example | Purpose |
| --- | --- | --- |
| `ConnectionStrings__AuthorizationDb` | `Data Source=/data/election-authorization.db` | SQLite file path |
| `CHAINBALLOT_AUTHZ_TRUSTEDAUTH_SIGNING_KEY` | `CHANGE_ME_...` | Trusted JWT signing key |
| `TrustedAuth__Issuer` | `cynetauth` | Expected trusted-authenticator token issuer |
| `TrustedAuth__Audience` | `chainballot-authz` | Expected token audience for this component |
| `Admin__SeedKey` | `CHANGE_ME_...` | Local seed endpoint guard |

### 3.3 Voter Component

| Variable | Example | Purpose |
| --- | --- | --- |
| `CHAINBALLOT_VOTER__COOKIENAME` | `.ChainBallot.Voter.Session` | Session cookie name |
| `CHAINBALLOT_VOTER__CYNETAUTHBASEURL` | `http://cynetauth:5015/` | Auth API base URL |
| `CHAINBALLOT_VOTER__ELECTIONAUTHORIZATIONBASEURL` | `http://authorization:5147/` | Authorization API base URL |
| `CHAINBALLOT_VOTER__QUEUEMODE` | `Kafka` | Queue transport mode |
| `CHAINBALLOT_VOTER__KAFKA__BOOTSTRAPSERVERS` | `redpanda:9092` | Kafka bootstrap servers |
| `CHAINBALLOT_VOTER__KAFKA__TOPIC` | `chainballot-ballots` | Submission topic |
| `CHAINBALLOT_VOTER__METADATAENDPOINTTEMPLATE` | `http://metadata:8088/elections/{electionId}.json` | Metadata URL template |
| `CHAINBALLOT_VOTER__DEFAULTELECTIONID` | `election-demo` | Default election context |
| `CHAINBALLOT_VOTER__BALLOTENCRYPTIONKEY` | `CHANGE_ME_...` | Ballot encryption key |
| `CHAINBALLOT_VOTER__SUBMISSIONOUTBOXPATH` | `/data/submission-outbox.json` | Durable outbox storage path |
| `CHAINBALLOT_VOTER__SUBMISSIONOUTBOXENTRYTTLMINUTES` | `1440` | Outbox entry TTL |
| `CHAINBALLOT_VOTER__SUBMISSIONOUTBOXMAXRETRYCOUNT` | `10` | Max retries before terminal failure |
| `CHAINBALLOT_VOTER__SUBMISSIONOUTBOXMAXENTRIES` | `1000` | Capacity cap |

### 3.4 Election Processing (Outbound)

| Variable | Example | Purpose |
| --- | --- | --- |
| `Processing__QueueMode` | `Kafka` | Queue adapter mode |
| `Processing__Kafka__BootstrapServers` | `redpanda:9092` | Kafka bootstrap servers |
| `Processing__Kafka__Topic` | `chainballot-ballots` | Consumer topic |
| `Processing__Kafka__ConsumerGroupId` | `chainballot-outbound` | Consumer group |
| `Processing__MaxBallotsPerBatch` | `32` | Batch closure count threshold |
| `Processing__MaxBatchWaitSeconds` | `20` | Batch closure wait threshold |
| `Processing__PollIntervalMilliseconds` | `1000` | Queue polling interval |
| `Processing__ProjectionPublishing__Enabled` | `true` | Auto-publish anchored evidence to transparency |
| `Processing__ProjectionPublishing__BaseUrl` | `http://transparency:5264` | Transparency API base URL |
| `Processing__ProverMode` | `DeterministicStub` | Prover strategy |
| `Processing__SettlementMode` | `SolanaRpc` | Settlement strategy |
| `Processing__SettlementNetwork` | `solana-localnet` | Settlement network label |
| `Processing__SettlementProgramId` | `chainballot-program-v2` | Program identifier label |
| `Processing__SettlementCommitment` | `confirmed` | Commitment label attached to a settlement reference (a configured label, not an observed on-chain confirmation — see `Prototype.md` §4.6) |
| `Processing__SolanaRpc__RpcEndpoint` | `http://solana-validator:8899` | Solana RPC endpoint |
| `Processing__SolanaRpc__RpcApiKey` | (unset) | Optional RPC API key/header value |
| `Processing__SolanaRpc__AnchorSubmitMethod` | `chainballot_anchorBatch` | Custom RPC method invoked for batch anchoring |
| `Processing__SolanaRpc__TimeoutSeconds` | `30` | Solana RPC call timeout |
| `Processing__Kafka__PollBatchSize` | `64` | Max messages read per Kafka poll |
| `Processing__Kafka__ConsumeTimeoutMilliseconds` | `100` | Kafka consume call timeout |
| `Processing__ProjectionPublishing__TimeoutSeconds` | `10` | Transparency ingest HTTP timeout |
| `Processing__ProjectionPublishing__LifecycleState` | `Open` | Lifecycle state stamped on published projection evidence |
| `Processing__ProjectionPublishing__AuthHeaderName` | `X-Transparency-Auth` | Header name used for the Transparency ingestion credential |
| `Processing__ProjectionPublishing__AuthToken` | `transparency-ingestion-dev-token` | Credential value; must match one of the target Transparency instance's configured ingestion credentials (`CHAINBALLOT_TRANSPARENCY__AUTHENTICATION__CREDENTIALS__N__TOKEN` with role `transparency.ingestion`), or every automatic evidence publish is rejected with 401 |
| `Processing__ProjectionPublishing__MetadataDigestEndpointTemplate` | `http://metadata:8088/digests/metadata/{electionId}.json` | Authoritative metadata digest lookup used to attach a digest to published evidence; should match Transparency's own `CHAINBALLOT_TRANSPARENCY__METADATADIGESTENDPOINTTEMPLATE` so both sides read the same value. Null/empty disables the lookup (evidence is published without a digest, which Transparency will flag stale if it has a configured authoritative digest for that election) |
| `Processing__ProjectionPublishing__AuditDigestEndpointTemplate` | `http://metadata:8088/digests/audit/{electionId}.json` | Authoritative audit digest lookup; see `MetadataDigestEndpointTemplate` |
| `Processing__ProjectionPublishing__DigestHttpTimeoutSeconds` | `10` | Timeout for authoritative digest HTTP lookups |
| `Processing__ExternalProver__Endpoint` / `ApiKey` / `TimeoutSeconds` | (unset) / (unset) / `30` | External HTTP prover strategy settings (used when `ProverMode=ExternalHttp`) |
| `Processing__LocalProcessProver__ExecutablePath` / `Arguments` / `WorkingDirectory` / `TimeoutSeconds` | (unset) / (unset) / (unset) / `60` | Local-process prover strategy settings (used when `ProverMode=LocalProcess`) |
| `Processing__Authentication__HeaderName` | `X-Processing-Auth` | Header name for operational-route auth |
| `Processing__Authentication__Credentials__0__Token` / `Name` / `Roles__0` | (unset) / (unset) / (unset) | Header-token credential bound to a role (`processing.operator` or `processing.ingest`); one entry per credential |

### 3.5 Transparency API

| Variable | Example | Purpose |
| --- | --- | --- |
| `CHAINBALLOT_TRANSPARENCY__PROJECTIONSTOREPATH` | `/data/projections.json` | Projection storage path |
| `CHAINBALLOT_TRANSPARENCY__AUTHORITATIVEDIGESTSOURCEMODE` | `Http` | Digest source mode |
| `CHAINBALLOT_TRANSPARENCY__METADATADIGESTENDPOINTTEMPLATE` | `http://metadata:8088/digests/metadata/{electionId}.json` | Metadata digest source |
| `CHAINBALLOT_TRANSPARENCY__AUDITDIGESTENDPOINTTEMPLATE` | `http://metadata:8088/digests/audit/{electionId}.json` | Audit digest source |
| `CHAINBALLOT_TRANSPARENCY__SOLANAREADMODE` | `Http` | Canonical state read mode |
| `CHAINBALLOT_TRANSPARENCY__SOLANAELECTIONSTATEENDPOINTTEMPLATE` | `http://solana-validator:8899` | Solana state endpoint |
| `CHAINBALLOT_TRANSPARENCY__SOLANAHTTPTIMEOUTSECONDS` | `10` | Timeout for canonical Solana HTTP reads |
| `CHAINBALLOT_TRANSPARENCY__MOCKSOLANAFINALIZEDSTATES__{electionId}` | `true` | Mock finalized-state override per election (used when `SolanaReadMode=Mock`) |
| `CHAINBALLOT_TRANSPARENCY__AUTHORITATIVEHTTPTIMEOUTSECONDS` | `10` | Timeout for authoritative digest HTTP calls |
| `CHAINBALLOT_TRANSPARENCY__AUTHORITATIVEMETADATADIGESTS__{electionId}` | (unset) | Local authoritative metadata digest override (used when digest source mode is `InMemory`) |
| `CHAINBALLOT_TRANSPARENCY__AUTHORITATIVEAUDITDIGESTS__{electionId}` | (unset) | Local authoritative audit digest override (used when digest source mode is `InMemory`) |
| `CHAINBALLOT_TRANSPARENCY__AUTOREINDEXENABLED` | `false` | Background reindex toggle |
| `CHAINBALLOT_TRANSPARENCY__AUTOREINDEXINTERVALSECONDS` | `60` | Auto-reindex polling interval |
| `CHAINBALLOT_TRANSPARENCY__AUTOREINDEXELECTIONIDS` | (unset) | Election ids included in periodic auto-reindex |
| `CHAINBALLOT_TRANSPARENCY__AUTHENTICATION__HEADERNAME` | `X-Transparency-Auth` | Header name for ingestion/reconciliation route auth |
| `CHAINBALLOT_TRANSPARENCY__AUTHENTICATION__CREDENTIALS__0__TOKEN` / `NAME` / `ROLES__0` | (unset) / (unset) / (unset) | Header-token credential bound to a role (`transparency.ingestion` or `transparency.reconciliation`); one entry per credential |

### 3.6 Result Portal

| Variable | Example | Purpose |
| --- | --- | --- |
| `CHAINBALLOT_RESULTPORTAL__TRANSPARENCYBASEURL` | `http://transparency:5264` | Transparency API base URL |
| `CHAINBALLOT_RESULTPORTAL__TRANSPARENCYTIMEOUTSECONDS` | `10` | Upstream timeout |
| `CHAINBALLOT_RESULTPORTAL__AUTHMODE` | `Open` | Portal auth mode (`Open`, `HeaderToken`, `Oidc`) |
| `CHAINBALLOT_RESULTPORTAL__HEADERAUTHHEADERNAME` | `X-ResultPortal-Auth` | Header name checked in `HeaderToken` mode |
| `CHAINBALLOT_RESULTPORTAL__HEADERAUTHTOKEN` | `CHANGE_ME_...` | Expected header value in `HeaderToken` mode |
| `CHAINBALLOT_RESULTPORTAL__HEADERAUTHROLES` | `result-reader` | Roles granted on a successful `HeaderToken` match |
| `CHAINBALLOT_RESULTPORTAL__OIDCAUTHORITY` | (unset) | OIDC authority URL (`Oidc` mode) |
| `CHAINBALLOT_RESULTPORTAL__OIDCCLIENTID` | (unset) | OIDC client id |
| `CHAINBALLOT_RESULTPORTAL__OIDCCLIENTSECRET` | (unset) | OIDC client secret |
| `CHAINBALLOT_RESULTPORTAL__OIDCRESPONSETYPE` | `code` | OIDC response type |
| `CHAINBALLOT_RESULTPORTAL__OIDCCALLBACKPATH` | `/signin-oidc` | OIDC sign-in callback path |
| `CHAINBALLOT_RESULTPORTAL__OIDCSIGNEDOUTCALLBACKPATH` | `/signout-callback-oidc` | OIDC sign-out callback path |
| `CHAINBALLOT_RESULTPORTAL__OIDCUSEPKCE` | `true` | Enables PKCE for the OIDC flow |
| `CHAINBALLOT_RESULTPORTAL__OIDCREQUIREHTTPSMETADATA` | `true` | Requires HTTPS metadata endpoint for OIDC |
| `CHAINBALLOT_RESULTPORTAL__OIDCSCOPES` | `openid,profile,email` | OIDC requested scopes |
| `CHAINBALLOT_RESULTPORTAL__DEFAULTELECTIONVISIBILITY` | `Public` | Visibility default |
| `CHAINBALLOT_RESULTPORTAL__PUBLICELECTIONIDS` | (unset) | Election ids always treated as public |
| `CHAINBALLOT_RESULTPORTAL__PRIVATEELECTIONIDS` | (unset) | Election ids treated as private |
| `CHAINBALLOT_RESULTPORTAL__PRIVATEELECTIONVIEWERROLE` | `result-reader` | Role required to view a private election |
| `CHAINBALLOT_RESULTPORTAL__HIDEPRIVATEELECTIONEXISTENCE` | `true` | Returns not-found instead of forbidden for private elections when unauthorized |

### 3.7 Watcher API

| Variable | Example | Purpose |
| --- | --- | --- |
| `CHAINBALLOT_WATCHER__TRANSPARENCYBASEURL` | `http://transparency:5264` | Transparency API base URL |
| `CHAINBALLOT_WATCHER__TRANSPARENCYTIMEOUTSECONDS` | `10` | Timeout for Transparency read calls |
| `CHAINBALLOT_WATCHER__DIGESTSOURCEMODE` | `Http` | Authoritative digest source mode |
| `CHAINBALLOT_WATCHER__DIGESTHTTPTIMEOUTSECONDS` | `5` | Timeout for authoritative digest HTTP calls |
| `CHAINBALLOT_WATCHER__METADATADIGESTENDPOINTTEMPLATE` | `http://metadata:8088/digests/metadata/{electionId}.json` | Metadata digest source |
| `CHAINBALLOT_WATCHER__AUDITDIGESTENDPOINTTEMPLATE` | `http://metadata:8088/digests/audit/{electionId}.json` | Audit digest source |
| `CHAINBALLOT_WATCHER__METADATADIGESTS__{electionId}` | (unset) | Local authoritative metadata digest override (used when digest source mode is `InMemory`) |
| `CHAINBALLOT_WATCHER__AUDITDIGESTS__{electionId}` | (unset) | Local authoritative audit digest override (used when digest source mode is `InMemory`) |
| `CHAINBALLOT_WATCHER__REPORTSTOREPATH` | `/data/reports.json` | Report storage path |
| `CHAINBALLOT_WATCHER__REPORTSIGNINGKEY` | `watcher-local-signing-key` | Evidence signing key |
| `CHAINBALLOT_WATCHER__OPERATORACCESSHEADERNAME` | `X-Operator-Key` | Operator header name |
| `CHAINBALLOT_WATCHER__OPERATORACCESSKEY` | `watcher-operator-key` | Operator header value |

### 3.8 Threshold Decryption API

| Variable | Example | Purpose |
| --- | --- | --- |
| `CHAINBALLOT_THRESHOLDDECRYPTION__STOREPATH` | `/data/ceremonies.json` | Ceremony storage path |
| `CHAINBALLOT_THRESHOLDDECRYPTION__DEFAULTREQUIREDAPPROVALS` | `2` | Default governance-approval threshold for new ceremonies |
| `CHAINBALLOT_THRESHOLDDECRYPTION__DEFAULTREQUIREDSHARES` | `3` | Default trustee-share threshold for new ceremonies |
| `CHAINBALLOT_THRESHOLDDECRYPTION__GOVERNANCEAPPROVERS` | `governor-a,governor-b,governor-c` | Recognized governance approver identifiers |
| `CHAINBALLOT_THRESHOLDDECRYPTION__ALLOWEDTRUSTEES` | `trustee-a,trustee-b,trustee-c,trustee-d,trustee-e` | Recognized trustee identifiers |
| `CHAINBALLOT_THRESHOLDDECRYPTION__OPERATORACCESSHEADERNAME` | `X-Operator-Key` | Operator header name |
| `CHAINBALLOT_THRESHOLDDECRYPTION__OPERATORACCESSKEY` | `decryption-operator-key` | Operator header value |
| `CHAINBALLOT_THRESHOLDDECRYPTION__TRANSPARENCYBASEURL` | `http://transparency:5264` | Transparency API base URL, used to confirm canonical election Closed/Finalized state before a ceremony may be proposed or finalized |

## 4. Docker Volumes Used for Durable State

| Volume | Purpose |
| --- | --- |
| `cynetauth-data` | CynetAuth SQLite store |
| `authorization-data` | Authorization SQLite store |
| `voter-data` | Voter durable outbox |
| `transparency-data` | Transparency projections |
| `watcher-data` | Watcher reports |
| `decryption-data` | Decryption ceremonies |
| `redpanda-data` | Broker state |
| `solana-ledger` | Local validator ledger state |

## 5. Configuration Hygiene Rules

1. Do not commit real signing keys or production secrets.
2. Keep local demo defaults only in Docker/dev configs.
3. Any new environment variable must be added in this file and in `README.md` documentation map references.
