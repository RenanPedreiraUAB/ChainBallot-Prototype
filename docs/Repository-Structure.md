# Chain Ballot Repository Structure

This guide defines the canonical way to navigate the repository and clarifies historical folder aliases.

## 1. Top-Level Layout (Canonical)

| Folder/File | Purpose |
| --- | --- |
| `Architecture.md` | Conceptual framework architecture |
| `Prototype.md` | Concrete implementation reference |
| `Test Plan and Validation.md` | Validation evidence and scenarios |
| `docs/` | Secondary reference docs (traceability, API, config, diagrams, structure, manual E2E testing guide) |
| `Configuration/` | Election configuration desktop component and tests |
| `CynetAuth/` | Trusted Third Party (TTP) authentication prototype instance |
| `Authorization/` | Election authorization API and tests |
| `Voter/` | Voter Component (historical alias: Inbound) |
| `ElectionProcessing/` | Election Processing Component (historical alias: Outbound) |
| `Transparency/` | Transparency and Audit Component API and tests |
| `Result/` | Result Portal web app and tests |
| `Watcher/` | Watcher independent audit API and tests |
| `Decryption/` | Threshold decryption orchestration API and tests |
| `Shared/` | Public contracts shared across components |
| `docker/` | Local stack scripts, metadata service (`metadata-server.mjs`) and data, seeding scripts |
| `k8s/` | Kubernetes scalability demonstration artifacts |
| `DIAGRAMS/` | Rendered UIS and component sequence diagrams (SVG) |
| `scripts/` | Repository-wide automation scripts |
| `artifacts/` | Generated validation evidence logs/artifacts |

## 2. Known Confusion Points (And Canonical Interpretation)

### 2.1 Nested Component Folder Names

Current examples:
- `ElectionProcessing/ElectionProcessing/ElectionProcessing.Api`
- `Voter/VoterComponent/VoterComponent.Web`
- `Configuration/ConfigurationComponent/`

Interpretation rule:
- Use the top-level folder as the authoritative component boundary.
- Treat deeper repeated names as implementation packaging details.

### 2.2 Naming Aliases in Documentation

| Canonical role name | Historical alias |
| --- | --- |
| Voter Component | Inbound |
| Election Processing Component | Outbound |
| Transparency and Audit Component | Transparency API / Transparency & Audit |
| Trusted Third Party (TTP) Authentication Service | CynetAuth instance |

Documentation rule:
- Use canonical role names first.
- Mention historical aliases only in parentheses where helpful.

## 3. Canonical Paths to Use in Docs and Reviews

When citing implementation locations, prefer these stable roots:
- `Configuration/ConfigurationComponent/`
- `CynetAuth/CynetAuth.Api/`
- `Authorization/ElectionAuthorization.Api/`
- `Voter/VoterComponent/VoterComponent.Web/`
- `ElectionProcessing/ElectionProcessing/ElectionProcessing.Api/`
- `Transparency/TransparencyAudit.Api/`
- `Result/ResultPortal.Web/`
- `Watcher/WatcherAudit.Api/`
- `Decryption/ThresholdDecryption.Api/`
- `Shared/ChainBallot.PublicContracts/`

## 4. Optional Future Cleanup (Non-Breaking Plan)

If desired, structure can be simplified in a future maintenance branch by flattening repeated folder names, for example:
- `ElectionProcessing/ElectionProcessing/ElectionProcessing.Api` -> `ElectionProcessing/ElectionProcessing.Api`
- `Voter/VoterComponent/VoterComponent.Web` -> `Voter/VoterComponent.Web`

This is optional and should be done only with coordinated project-file and CI path updates.
