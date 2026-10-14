# Chain Ballot Traceability Matrix

This matrix links architectural roles and UIS flows to concrete prototype components, key interfaces/endpoints, and validation evidence.

## 1. Role Traceability

| Architectural role | Prototype instance | Key implementation location | Primary validation evidence |
| --- | --- | --- | --- |
| Election Configuration Component | ConfigurationComponent | `Configuration/ConfigurationComponent/` | TS-00 in Test Plan section 5.0 |
| Trusted Third Party (TTP) Authentication Service | CynetAuth (prototype instance) | `CynetAuth/CynetAuth.Api/` | TS-01 in Test Plan section 5.0 |
| Election Authorization Component | ElectionAuthorization.Api | `Authorization/ElectionAuthorization.Api/` | TS-02 in Test Plan section 5.0 |
| Voter Component (historical alias: Inbound) | VoterComponent.Web | `Voter/VoterComponent/VoterComponent.Web/` | TS-03 in Test Plan section 5.0 |
| Message Broker / Queue Infrastructure | Redpanda | `docker-compose.local.yml` (`redpanda`) | E2E-01 in Test Plan section 5.3 |
| Election Processing Component (historical alias: Outbound) | ElectionProcessing.Api | `ElectionProcessing/ElectionProcessing/ElectionProcessing.Api/` | TS-04 in Test Plan section 5.0 |
| Canonical Settlement Ledger | Solana local validator (prototype instance) | `docker-compose.local.yml` (`solana-validator`) | E2E-02 in Test Plan section 5.3 |
| Transparency & Audit Component | TransparencyAudit.Api | `Transparency/TransparencyAudit.Api/` | TS-05 in Test Plan section 5.0 |
| Result Portal | ResultPortal.Web | `Result/ResultPortal.Web/` | TS-06 in Test Plan section 5.0 |
| Watcher / Independent Audit Component | WatcherAudit.Api | `Watcher/WatcherAudit.Api/` | TS-07 in Test Plan section 5.0 |
| Threshold Decryption Orchestration Component | ThresholdDecryption.Api | `Decryption/ThresholdDecryption.Api/` | TS-08 in Test Plan section 5.0 |
| Shared Public Data Contracts | ChainBallot.PublicContracts | `Shared/ChainBallot.PublicContracts/` | Cross-component DTO usage in E2E-01 to E2E-05 |

## 2. UIS Flow Traceability

| UIS | Diagram | Architecture section | Prototype section | Key runtime endpoint surfaces | Validation evidence |
| --- | --- | --- | --- | --- | --- |
| UIS-01 Voter Authentication and Authorization | `DIAGRAMS/HL UIS-01.svg` | Architecture section 2.2, 3.2, 3.3, 3.4 | Prototype section 4.2, 4.3, 4.4 | `/api/auth/*`, `/api/authorization/*` | TS-01, TS-02, TS-03, E2E-01 |
| UIS-02 Ballot Casting and Submission | `DIAGRAMS/HL UIS-02.svg` | Architecture section 2.2, 3.4, 3.5, 3.6 | Prototype section 4.4, 4.5, 4.6, 5.3 | `/api/processing/ingest`, `/api/authorization/elections/{electionId}/consume` | TS-03, TS-04, E2E-01 |
| UIS-03 Individual Vote Verification | `DIAGRAMS/HL UIS-03.svg` | Architecture section 2.2, 3.8, 3.9 | Prototype section 4.8, 4.9 | `/api/transparency/elections/{electionId}/verify/{vid}` | TS-05, TS-06, E2E-04 |
| UIS-04 Election Configuration and Initialization | `DIAGRAMS/HL UIS-04.svg` | Architecture section 2.2, 3.1 | Prototype section 4.1 | Configuration desktop workflow + metadata publication | TS-00 |
| UIS-05 Election Auditing | `DIAGRAMS/HL UIS-05.svg` | Architecture section 2.2, 3.10 | Prototype section 4.10 | `/api/watcher/elections/{electionId}/run-audit` | TS-07, E2E-05 |
| UIS-06 Closing, Decryption and Publication | `DIAGRAMS/HL UIS-06.svg` | Architecture section 2.2, 3.11 | Prototype section 4.11 | `/api/decryption/elections/{electionId}/ceremony*`, `/api/transparency/elections/{electionId}/ingest` | TS-08, E2E-02, E2E-03 |

## 3. Baseline vs Stress Evidence Traceability

| Evidence type | Baseline source | Stress-only source | Reporting rule |
| --- | --- | --- | --- |
| Kubernetes autoscaling | `k8s/chainballot-autoscale-demo.yaml` and `k8s/spike-ingest.ps1` | `k8s/keda-outbound-kafka-scaledobject.yaml` plus induced lag/backlog actions | Report baseline and stress results separately |
| Validation interpretation | Test Plan sections 5.5 and 5.6 | Test Plan sections 5.5 and 5.6 | Never present stress-induced scale events as baseline behavior |

## 4. Maintainer Rules

1. If a new endpoint is added, update both this matrix and `docs/API-Reference.md` in the same change.
2. If a UIS sequence changes, regenerate and replace the corresponding `DIAGRAMS/HL UIS-0X.svg` file and update this matrix.
3. If a test suite is renamed or split, update the validation IDs in this matrix and in `Test Plan and Validation.md`.
