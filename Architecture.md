# Chain Ballot — Framework Architecture

## 0. How To Read This Document

Chain Ballot is described across two companion documents that intentionally answer two different questions:

| Document | Question it answers | Nature |
| --- | --- | --- |
| **Architecture.md** (this document) | *What is the ideal, target design of the Chain Ballot framework, and why?* | Conceptual. Technology-agnostic. Describes architectural roles, protocols, data contracts, trust boundaries and threat models. |
| **[Prototype.md](Prototype.md)** | *How was one concrete instance of that framework actually built, and how was it validated?* | Implementation-oriented. Describes the reference codebase, its technology choices, its scope, and its test/validation evidence. |

### 0.1 Architecture vs Prototype — the conceptual distinction

This distinction is foundational and is used consistently everywhere in this repository's documentation:

- The **Architecture** is the *ideal, desirable framework* — the set of roles, responsibilities, protocols, data contracts and trust rules that any correct implementation of Chain Ballot must satisfy. It is technology-agnostic: it does not mandate a specific programming language, database engine, UI framework, or hosting model.
- The **Prototype** is *one concrete, running instance* of that architecture. Its purpose is to:
  1. **Validate** that the architecture is implementable and internally consistent (every architectural role has a working realization, every interaction/flow is executable end to end, every trust boundary is enforceable).
  2. **Demonstrate** configurability and scalability properties of the framework (e.g., pluggable proof/queue/settlement strategies, horizontal scaling of processing consumers).
  3. Stand as an **open-source contribution in its own right**: a cloneable "base project" that fully works locally for demonstration/testing and that a future adopter can extend or harden toward a production deployment.

A direct consequence of this split is a **naming distinction** that recurs throughout this document and the codebase:

> An architectural role describes *what a component must do*. A prototype instance is the *concrete thing that plays that role in this repository*.
>
> Example: **"Trusted Third Party (TTP) Authentication Service"** is an architectural role — an external identity boundary that authenticates a real-world identity and hands back a trusted assertion. **CynetAuth** is the prototype's concrete instance of that role: a small, self-issued JWT identity service built for local, offline execution. A production adopter could replace CynetAuth with an enterprise IdP (Entra ID, Auth0, Keycloak, …) without changing the architecture — only the instance changes.

The same pattern applies to every component in this document. Architecture.md always names the *role*; where useful, a short note points to the *prototype instance* documented in full in [Prototype.md](Prototype.md).

### 0.2 Scope of this document

This document covers the full election lifecycle at the system level:

- Election provisioning and initialization.
- Voter authentication and voting authorization.
- Ballot submission and batching.
- Settlement anchoring on the canonical ledger.
- Result publication.
- Individual vote verification.
- Independent (watcher) auditing.
- Post-close threshold decryption and final tally publication.

This document is intentionally explicit about trust boundaries and data authority, to prevent inconsistencies between presentation views, off-chain repositories, and canonical ledger state.

This document does **not** describe: specific programming languages/frameworks, specific persistence engines, specific container/orchestration tooling, or specific test suites. Those are Prototype.md concerns.

---

## 1. Core Architectural Principles

### 1.1 Canonical Authority
The blockchain settlement ledger is the canonical authority for election settlement state and final result integrity. Every other store in the system is a projection of, or an accelerator for, this canonical state — never an independent source of truth.

### 1.2 Separation of Write and Read Concerns
- **Write path:** Voter Component → Message Broker → Election Processing Component → Canonical Settlement Ledger.
- **Read path:** Result Portal / Watcher Component → Transparency & Audit Component → Canonical Settlement Ledger + repositories.

### 1.3 Reusable Smart Contract Program
Election provisioning initializes a reusable election program instance with election-specific parameters. Dynamic, per-election contract code generation is explicitly disallowed — this keeps the on-chain surface auditable and reduces the smart-contract attack surface to a single, reviewable program.

### 1.4 Privacy by Design
- Real identity never propagates into the vote settlement flow.
- A Virtual Identity (VID) is used as a privacy-preserving identity surrogate for all election operations after authentication.
- Ballots are encrypted at submission time.
- Threshold decryption occurs only after election close, and no single actor can unilaterally decrypt ballots.

### 1.5 Verifiable Transparency
All user-facing vote/result views must be cryptographically or referentially tied to canonical ledger settlements. Off-chain repositories are projections/indexes, never independent truth sources.

### 1.6 Auditability and Non-Repudiation
Every major operation emits evidence that can be cross-checked by an independent auditor (the Watcher role) against canonical settlement references.

### 1.7 Configurability
Every seam that has more than one reasonable production/demo answer (proof generation, settlement anchoring, queue transport, presentation authentication mode) is expressed as a **strategy/adapter boundary** in the architecture, not a single hard-wired choice. This is what allows the same framework to run as a fully local demonstrator and, later, as a production deployment, without redesign.

---

## 2. High-Level Architecture

### 2.1 Component Catalogue (Architectural Roles)

| # | Architectural Role | Responsibility | Owns (data) |
| --- | --- | --- | --- |
| 1 | **Election Configuration Component** | Provision elections, validate parameters, generate and hash metadata, initialize the reusable election program, produce scale/deployment recommendations. | Election definition (Draft state), deployment manifest |
| 2 | **Trusted Third Party (TTP) Authentication Service** *(external)* | Authenticate real-world voter identity; issue a trusted identity assertion. | Real-identity credentials (outside election trust boundary) |
| 3 | **Election Authorization Component** | Bridge to the TTP; create/retrieve the VID for an authenticated subject; track per-election eligibility; issue and atomically consume one-time voting grants. | VID Repository, Voting Authorization Repository |
| 4 | **Voter Component** *(Inbound)* | Voter-facing UI; triggers authentication/authorization; fetches election metadata; encrypts the ballot; builds and submits the ballot package. | Transient session state only |
| 5 | **Message Broker / Queue Infrastructure** | Durable buffer decoupling voter submission bursts from processing throughput. | In-flight submission messages |
| 6 | **Election Processing Component** *(Outbound)* | Consume ballots; validate structure and replay/duplicate rules; batch commitments; build Merkle proofs; anchor settlement on the canonical ledger; emit audit/read-model evidence. | Runtime batch/tally state, audit evidence |
| 7 | **Canonical Settlement Ledger** *(Smart Contract Platform)* | Canonical election lifecycle state, anchored batch roots/proof references, final tally references. | Canonical settlement state |
| 8 | **Transparency & Audit Component** | Build verifiable read projections for result publication, VID verification and audit evidence; reconcile against canonical ledger state. | Verified projection / read model |
| 9 | **Result Portal** | Public/private presentation of election outcomes and individual vote verification. | None (pure consumer of Transparency & Audit Component) |
| 10 | **Watcher / Independent Audit Component** | Independent, operator-facing supervision that cross-validates metadata, audit evidence, the transparency read model, and canonical settlement records; produces signed inconsistency/consistency evidence. | Signed audit reports |
| 11 | **Threshold Decryption Orchestration Component** | Post-close governance ceremony: approvals + trustee key shares + finalization of decrypted tallies + final tally publication reference. | Ceremony state |
| 12 | **Election Metadata Repository** | Published, canonical election definitions (candidates, timing, contract reference). | Published election metadata |
| 13 | **Audit Repository** | Indexed processing/settlement evidence available to the Watcher role. | Batch/settlement evidence |
| 14 | **Shared Public Data Contracts** | Canonical cross-component handoff contracts (see §2.4). | N/A (contract definitions only) |

> Naming note: Roles #1, #3–#6, #8–#11 all have one concrete prototype instance documented in [Prototype.md § 2](Prototype.md#2-component-instance-map). Role #2 (TTP) and role #7 (Canonical Settlement Ledger) are, by design, pluggable external dependencies; the prototype's instances of them (CynetAuth, and a Solana devnet/local-validator target) are documented in Prototype.md as reference implementations, not architectural requirements.

### 2.2 High-Level User Interaction Scenarios (UIS)

The architecture is validated end to end through six user interaction scenarios (UIS). Each UIS has a corresponding sequence diagram in [`DIAGRAMS/`](DIAGRAMS/), kept implementation-accurate.

| UIS | Title | Primary roles involved | Diagram |
| --- | --- | --- | --- |
| UIS-01 | Voter Authentication and Voting Authorization | Voter Component, TTP, Election Authorization Component | [`DIAGRAMS/HL UIS-01.svg`](DIAGRAMS/HL%20UIS-01.svg) |
| UIS-02 | Ballot Casting and Vote Submission | Voter Component, Election Authorization Component, Message Broker, Election Processing Component, Canonical Settlement Ledger, Transparency & Audit Component | [`DIAGRAMS/HL UIS-02.svg`](DIAGRAMS/HL%20UIS-02.svg) |
| UIS-03 | Individual Vote Verification | Result Portal, Transparency & Audit Component | [`DIAGRAMS/HL UIS-03.svg`](DIAGRAMS/HL%20UIS-03.svg) |
| UIS-04 | Election Configuration and Initialization | Election Configuration Component, Canonical Settlement Ledger, Election Metadata Repository | [`DIAGRAMS/HL UIS-04.svg`](DIAGRAMS/HL%20UIS-04.svg) |
| UIS-05 | Election Auditing | Watcher Component, Transparency & Audit Component | [`DIAGRAMS/HL UIS-05.svg`](DIAGRAMS/HL%20UIS-05.svg) |
| UIS-06 | Election Closing, Threshold Decryption and Result Publication | Threshold Decryption Component, Transparency & Audit Component, Result Portal | [`DIAGRAMS/HL UIS-06.svg`](DIAGRAMS/HL%20UIS-06.svg) |

A single logical/component view of the whole framework is available in [`DIAGRAMS/HL_LogicView.svg`](DIAGRAMS/HL_LogicView.svg).

### 2.3 High-Level Interactions and Process Flow

At a logical level (see [`DIAGRAMS/HL_LogicView.svg`](DIAGRAMS/HL_LogicView.svg)), the framework is organized into three horizontal layers:

1. **Provisioning layer** — the Election Configuration Component validates and publishes election metadata and initializes the reusable election program on the canonical ledger, before any voter interaction can occur.
2. **Write (submission) pipeline** — Voter Component → (Trusted Third Party + Election Authorization Component for identity/eligibility) → Message Broker → Election Processing Component → Canonical Settlement Ledger. This pipeline is intentionally decoupled at the broker boundary so that voter-facing burst traffic never applies backpressure directly onto settlement anchoring.
3. **Read (transparency) pipeline** — Election Processing Component emits evidence into the Audit Repository and into the Transparency & Audit Component; Result Portal and Watcher Component are pure consumers of the Transparency & Audit Component, never of raw repositories directly.

A fourth, cross-cutting flow governs **election closing**: after the Canonical Settlement Ledger reflects the election as closed, the Threshold Decryption Orchestration Component runs a governance ceremony (approvals + trustee shares) before the final tally is published into the Transparency & Audit Component, making it available to the Result Portal and to the Watcher Component.

### 2.4 Data Model (Shared Public Contracts)

The framework defines a small set of canonical, cross-component data contracts. These are the *only* objects that are allowed to cross component boundaries for election-flow data — internal component-local models must map to/from these contracts, never leak past the boundary directly. This is what keeps trust assumptions between components explicit and reviewable.

#### 2.4.1 Identity and Authorization Contracts

| Contract | Fields | Purpose |
| --- | --- | --- |
| `VoterAuthorizationProfile` | `Subject`, `Vid`, `CreatedAtUtc` | The stable TTP subject mapped to its Virtual Identity (VID). |
| `ElectionAuthorizationState` | `ElectionId`, `Vid`, `Status`, `VotedAtUtc` | Per-election eligibility/consumption state for a VID. `Status` ∈ `{EligibleNotVoted, AlreadyVoted, NotEligible}`. |
| `AuthorizationGrant` | `GrantId`, `ElectionId`, `Vid`, `IssuedAtUtc`, `ExpiresAtUtc` | A one-time, time-bounded permission to submit exactly one ballot. |
| `ConsumeAuthorizationRequest` / `ConsumeAuthorizationResponse` | `GrantId`, `SubmissionNonce` / `Outcome`, `ConsumedAtUtc`, `Message` | Atomic, one-time consumption of a grant. `Outcome` ∈ `{Consumed, AlreadyVoted, InvalidGrant, ExpiredGrant, NotEligible}`. |

#### 2.4.2 Ballot Submission Contracts

| Contract | Fields | Purpose |
| --- | --- | --- |
| `EncryptedBallotPayload` | `Algorithm`, `NonceBase64`, `TagBase64`, `CiphertextBase64` | Opaque, authenticated-encryption envelope for the vote content. The Election Processing Component never decrypts this payload — it only anchors it. |
| `BallotSubmissionPackage` | `ElectionId`, `Vid`, `BallotCommitment`, `SubmissionNonce`, `BallotNullifier`, `EncryptedVotePayload`, `CreatedAtUtc` | The canonical handoff envelope from the Voter Component to the Message Broker / Election Processing Component. |
| `QueueReceipt` | `QueueItemId`, `QueuedAtUtc`, `QueueMode` | Acknowledgement contract so the Voter Component can confirm durable hand-off to the broker. |

**Canonical nullifier rule (frozen architectural decision):**

$$ \text{BallotNullifier} = \text{SHA256}(\;\texttt{"\{ElectionId\}|\{Vid\}|\{SubmissionNonce\}"}\;) \text{, lower-case hex} $$

This derivation is authoritative for replay prevention. The Election Processing Component rejects a submission whenever: the nullifier is missing/malformed, does not match the canonical derivation, duplicates another nullifier within the same batch, or duplicates a nullifier already accepted in a previously anchored batch.

#### 2.4.3 Settlement and Proof Contracts

| Contract | Fields | Purpose |
| --- | --- | --- |
| `SettlementReference` | `Network`, `ProgramId`, `ElectionId`, `TransactionSignature`, `Slot`, `BlockTimeUtc`, `Commitment` | Canonical pointer to a specific on-chain settlement event. This is the atomic "proof of anchoring" reference reused everywhere a claim needs to be tied to chain truth. |
| `InclusionProofReference` | `BatchId`, `MerkleRoot`, `LeafCommitment`, `LeafIndex`, `ProofUri`, `ProofHash` | Points a single ballot commitment to its position/proof within an anchored batch's Merkle tree. |
| `BatchAnchorReference` | `ElectionId`, `BatchId`, `BatchSequence`, `BallotCount`, `BatchRoot`, `ProofSystem`, `ProofReference`, `SettlementReference`, `AnchoredAtUtc` | The complete anchoring record for one processed batch — the unit of settlement evidence produced by the Election Processing Component. |

#### 2.4.4 Result and Verification Contracts

| Contract | Fields | Purpose |
| --- | --- | --- |
| `VerifiedResultSnapshot` | `ElectionId`, `ResultDigest`, `LifecycleState`, `SettlementReference`, `CandidateTallies`, `GeneratedAtUtc` | The single object the Result Portal is allowed to render as "the result". |
| `VidVerificationResponse` | `ElectionId`, `Vid`, `Outcome`, `Message`, `SettlementReference`, `InclusionProofReference`, `CheckedAtUtc`, `ConsistencyDigest` | The response to "was my VID's ballot included and settled". `Outcome` ∈ `{Verified, NotFound, Inconsistent, ElectionNotFinalized, Error}`. |

### 2.5 Election Lifecycle States

The election, as a whole, moves through a single canonical lifecycle, tracked partly off-chain (administrative) and partly on-chain (settlement-relevant):

`Draft → Deployed/Initialized → Open (accepting ballots) → Closed → Decrypting → Finalized`

- **Draft → Deployed/Initialized**: owned by the Election Configuration Component (§4.1).
- **Open → Closed**: driven by the election's configured time window and enforced by the Election Processing Component/Canonical Settlement Ledger.
- **Closed → Decrypting → Finalized**: owned by the Threshold Decryption Orchestration Component, with `Finalized` only reachable once the Transparency & Audit Component ingests the published final tally (§6).

---

## 3. Low-Level / Internal Component Design

This section describes, for every architectural role, its internal paradigm, its process flow, its in/out communications, its threat model, and any technical aspect that required a specific design decision to solve a real problem (not merely "how it happens to be coded").

### 3.1 Election Configuration Component

**Architectural paradigm:** an offline/administrative provisioning tool. It is not part of the runtime voting path — once an election is deployed, this component's artifacts (metadata, deployment manifest, canonical program reference) become read-only inputs to every other component.

**Internal process flow:** see UIS-04. Configure → validate → generate metadata → hash metadata → persist deployment manifest → derive scale recommendations → initialize the reusable election program on the canonical ledger → publish metadata + contract reference.

**Communications:**
- **In:** administrator-provided election definition (name, schedule, candidates, contract parameters).
- **Out:** published metadata (to the Election Metadata Repository), a program-initialization call (to the Canonical Settlement Ledger).
- **Payload types:** canonical election metadata document (candidate list, schedule, cryptographic policy references), a deployment manifest (see §5.3), a metadata hash.
- **Protocol:** metadata publication is a simple document-publish operation (HTTP PUT/POST semantics); ledger initialization is a single program-initialization transaction.

**Threat model & mitigations:**
- *Tampering with published metadata after deployment* → metadata is hashed (SHA-256) at publish time, and the hash is bound into the deployment manifest and the on-chain program reference, so any later mutation is detectable by hash mismatch.
- *Invalid/ambiguous election definitions reaching later stages* → all election rules (candidate count/uniqueness, schedule ordering, lifecycle-state gating) are validated **before** metadata generation; an invalid draft can never be deployed.

**Unique technical aspects:**
- **Deployment manifest** as a stable, versioned handoff artifact (schema-versioned) that every downstream component (Processing, Transparency, Watcher) can treat as the single source of truth for "what was configured", instead of re-deriving assumptions independently.
- **Scale recommendation model**: derives suggested consumer/partition/batch-size parameters from expected voter volume, election duration and peak-participation assumptions, so operational configuration is informed by the same values used to validate the election.

### 3.2 Trusted Third Party (TTP) Authentication Service *(external role)*

**Architectural paradigm:** an external identity boundary. The framework treats it as untrusted-but-verifiable: Chain Ballot components never store or forward raw credentials, only a signed identity assertion.

**Internal process flow:** see UIS-01, first phase (credential submission → assertion issuance).

**Communications:**
- **In:** real-world credentials (from the voter, via the Voter Component).
- **Out:** a signed identity assertion (subject + signature), consumable without a synchronous callback.
- **Protocol:** a login/assertion exchange over an authenticated channel; the resulting assertion is a bearer token verifiable offline by its signature.

**Threat model & mitigations:**
- *Assertion forgery* → the assertion is cryptographically signed with a key trusted by the Election Authorization Component; signature/issuer validation happens locally, with no implicit trust of an unsigned claim.
- *Credential leakage into the election trust boundary* → by design, no component downstream of the TTP ever receives or stores the raw credential, only the signed assertion.

### 3.3 Election Authorization Component

**Architectural paradigm:** a stateless-per-request authorization boundary backed by durable per-VID/per-election state.

**Internal process flow:** see UIS-01 (second phase) and UIS-02 (grant issue/consume).

**Communications:**
- **In:** a validated TTP assertion (bearer), election id, grant-consumption requests.
- **Out:** `VoterAuthorizationProfile`, `ElectionAuthorizationState`, `AuthorizationGrant`, `ConsumeAuthorizationResponse` (§2.4.1).
- **Protocol:** synchronous request/response (profile lookup, eligibility lookup, grant issue, grant consume).

**Threat model & mitigations:**
- *Double voting / race conditions on concurrent submission* → grant consumption is an **atomic, one-time state transition**: only one concurrent consumption attempt for the same grant can succeed; all others observe `AlreadyVoted`/`InvalidGrant`. This is the architectural control that guarantees "one grant → at most one counted ballot" even under concurrent load.
- *Grant replay/reuse* → grants are single-use and time-bounded (`ExpiresAtUtc`); consumption requires both the grant id and a client-generated submission nonce for traceability.
- *Identity correlation across elections* → the VID is stable per subject but is the only identity surrogate ever exposed to election-flow components; real identity never appears past this boundary.

**Unique technical aspects:**
- **VID mapping**: a one-way, stable mapping from TTP subject → VID, created lazily on first authorization request and reused thereafter, so the same voter always resolves to the same VID without re-exposing the real identity to downstream components.

### 3.4 Voter Component *(Inbound)*

**Architectural paradigm:** a stateful, session-scoped presentation/preparation layer. It is the only component that ever handles ballot plaintext, and only transiently, in memory, before encryption.

**Internal process flow:** see UIS-01 (metadata/eligibility) and UIS-02 (encryption, packaging, submission).

**Communications:**
- **In:** voter's candidate selection; TTP assertion; election metadata; authorization grant.
- **Out:** `BallotSubmissionPackage` (§2.4.2) to the Message Broker; grant-consumption request to the Election Authorization Component.
- **Protocol:** synchronous request/response toward TTP/Authorization/Metadata; asynchronous publish toward the Message Broker.

**Threat model & mitigations:**
- *Ballot content exposure in transit or at the broker* → the vote is encrypted (authenticated symmetric encryption) before it ever leaves the Voter Component; the broker and processing pipeline only ever see ciphertext plus the commitment/nullifier.
- *Submission loss under broker unavailability* → the architecture requires a durable, retryable local submission record (an "outbox") so a transient broker outage cannot silently drop an already-authorized vote; the grant is only consumed once the submission itself is durably queued.
- *Ballot tampering after encryption* → a ballot commitment (a hash binding of the submission) is generated alongside the ciphertext, so any later tampering with the anchored data is detectable against the committed value.

**Unique technical aspects:**
- **Ballot commitment and nullifier computation** happen in the Voter Component at submission time (§2.4.2), binding the submission to a specific election/VID/nonce triple before it ever reaches the processing pipeline.
- **Auth-provider abstraction**: authentication is modeled as a pluggable boundary so the TTP instance (§3.2) can be swapped without changing the rest of the component.

### 3.5 Message Broker / Queue Infrastructure

**Architectural paradigm:** a durable, ordered publish/consume boundary. It exists purely to decouple voter-facing submission bursts from the processing pipeline's throughput.

**Communications:**
- **In:** `BallotSubmissionPackage` messages from the Voter Component.
- **Out:** the same messages, delivered to the Election Processing Component's consumer group.
- **Protocol:** topic-based publish/subscribe with consumer-group semantics (at-least-once delivery expected by consumers).

**Threat model & mitigations:**
- *Consumer-side duplicate processing* (a natural consequence of at-least-once delivery) → mitigated entirely downstream, by the Election Processing Component's nullifier/duplicate checks (§3.6), not by the broker itself.
- *Backlog-driven denial of service on the processing pipeline* → the broker is the natural point for horizontal elasticity (more consumers) and for the framework's queue-lag–based autoscaling strategy (§7 scalability discussion in Prototype.md).

### 3.6 Election Processing Component *(Outbound)*

**Architectural paradigm:** a continuously running batch pipeline (ingress → validate → batch → prove → anchor → publish evidence), designed so that a failure isolated to one election never blocks others.

**Internal process flow:** see UIS-02 (second phase, from broker delivery to settlement + evidence publication).

**Communications:**
- **In:** `BallotSubmissionPackage` messages from the Message Broker; administrative pause/resume/retry requests.
- **Out:** a settlement-anchoring transaction to the Canonical Settlement Ledger (`BatchAnchorReference`, §2.4.3); projection evidence to the Transparency & Audit Component; failure/audit evidence to the Audit Repository.
- **Protocol:** asynchronous consumption from the broker; a settlement transaction toward the ledger; an evidence-ingest call toward the Transparency & Audit Component.

**Threat model & mitigations:**
- *Replay / double counting* → canonical nullifier rule enforcement (§2.4.2), both within a batch and against all previously accepted state.
- *Invalid or forged proof reaching settlement* → the generated batching proof is **verified before** the settlement transaction is submitted; a failed verification blocks anchoring for that batch instead of anchoring unverified data.
- *One election's failure cascading to all elections* → failure isolation is scoped per election: a blocked/failing election's queue is paused for retry while other elections continue to be processed normally.
- *Partial/uncommitted settlement being treated as final* → a batch is only considered settled once the ledger confirms the transaction; no partial or client-side state is ever treated as canonical.

**Unique technical aspects:**
- **Batching**: ballot commitments are grouped into batches closed by either a configurable maximum count or a maximum wait time — a latency/throughput trade-off exposed as a configuration seam.
- **Merkle tree over ballot commitments**: each batch's commitments are organized into a Merkle tree; the root is anchored, and each ballot's `InclusionProofReference` (§2.4.3) lets any individual ballot be proven a member of that anchored root without revealing other ballots.
- **Proof generation and verification** are modeled as independent, replaceable strategies (any correct proof system can be substituted without changing the rest of the pipeline), with verification always required before anchoring.
- **Settlement anchoring** is modeled as a replaceable adapter to the Canonical Settlement Ledger, so the same pipeline logic works against any ledger that can accept a root+proof+metadata anchoring transaction.

### 3.7 Canonical Settlement Ledger *(Smart Contract Platform)*

**Architectural paradigm:** an external, canonical, append-only settlement authority (a smart-contract platform). See §4 for the full contract/blockchain-interaction model.

**Threat model & mitigations:**
- *Off-chain data diverging from chain truth* → resolved by the conflict-resolution rule in §6.3: on-chain state always wins; divergent off-chain projections are marked stale and reconciled, never silently overridden.

### 3.8 Transparency & Audit Component

**Architectural paradigm:** a verifiable read-model projector. It never originates truth; it only projects and reconciles canonical ledger state, published metadata, and processing evidence into query-friendly, verifiable views.

**Internal process flow:** see UIS-02 (evidence ingestion), UIS-03 (VID verification), UIS-06 (finalization ingestion).

**Communications:**
- **In:** evidence-ingestion calls from the Election Processing Component and from the election-closing flow; snapshot/verification/canonical-state queries from the Result Portal and Watcher Component.
- **Out:** `VerifiedResultSnapshot`, `VidVerificationResponse`, projection/consistency status, canonical settlement state.
- **Protocol:** synchronous request/response for all read and ingest operations.

**Threat model & mitigations:**
- *Displaying results that are not actually anchored on-chain* → every returned snapshot/verification response carries a `SettlementReference`; there is no code path that returns a result without one once an election is finalized.
- *Silent drift between the projection and canonical state* → the projection is marked stale and reconciled whenever an authoritative digest (metadata/audit) mismatch is detected, rather than being trusted indefinitely.
- *Conflicting claims between operators/auditors about the true state* → a formal reconciliation workflow (dispute → override request → resolution) with an immutable case-event trail exists specifically to make disagreements auditable rather than silently overwritten.

**Unique technical aspects:**
- **Deterministic consistency digests**: a digest computed over projection contents lets any consumer (Result Portal, Watcher) detect, without re-deriving the whole state, whether the projection they read matches the authoritative digest sources.
- **Reindex / re-sync**: the projection can be rebuilt from canonical sources on demand or on a triggered re-sync event, rather than requiring manual data repair.

### 3.9 Result Portal

**Architectural paradigm:** a thin, policy-gated presentation layer. It is architecturally forbidden from defining truth — every value it displays is sourced from the Transparency & Audit Component.

**Internal process flow:** see UIS-03 and UIS-06 (second phase).

**Communications:**
- **In:** voter/observer requests (by election id, by VID).
- **Out:** requests to the Transparency & Audit Component only.
- **Protocol:** synchronous request/response.

**Threat model & mitigations:**
- *Unauthorized disclosure of private-election results* → an election visibility policy (public vs. private, with a configurable authentication mode) gates access before any Transparency query is made.
- *Portal becoming a second source of truth* → architecturally disallowed: the portal holds no independent repository of its own.

### 3.10 Watcher / Independent Audit Component

**Architectural paradigm:** an independent, operator-triggered cross-validator. It is intentionally a *consumer* of the same read model the Result Portal uses, plus an independent authoritative digest source, so it can catch divergence between what is published and what is authoritative.

**Internal process flow:** see UIS-05.

**Communications:**
- **In:** an audit-run request (by election id), scoped to authorized operators.
- **Out:** a structured, signed audit report; an evidence export (report digest + signature).
- **Protocol:** a single synchronous audit-run request that internally fans out to the Transparency & Audit Component and to the authoritative digest source, then returns one consolidated report.

**Threat model & mitigations:**
- *Unauthorized or unattributed audit report tampering* → reports are digested and signed at generation time, so any later tampering with a stored report is detectable, and evidence exports carry that signature.
- *Audit endpoints being publicly reachable* → non-health audit endpoints require operator-level access; auditing is a trusted-operator capability, not a public one.

**Unique technical aspects:**
- **Multi-source correlation**: lifecycle consistency, settlement integrity and metadata/audit digest consistency are each evaluated as independent checks and combined into one report, so a failure is attributable to a specific check rather than a single opaque pass/fail.

### 3.11 Threshold Decryption Orchestration Component

**Architectural paradigm:** a post-close governance ceremony orchestrator. It formalizes "no single actor can unilaterally decrypt/finalize a result" as a state machine with explicit governance gates.

**Internal process flow:** see UIS-06 (first phase).

**Communications:**
- **In:** ceremony proposal (required approvals/shares), governance approvals, trustee key-share submissions, finalization request (with the decrypted/final tallies), publication request.
- **Out:** ceremony state transitions; a deterministic result digest; a final-tally publication reference; (via the requesting operator) a finalization ingest call to the Transparency & Audit Component.
- **Protocol:** a sequence of synchronous, operator-scoped governance calls, gated by configurable approval and share thresholds.

**Threat model & mitigations:**
- *Unilateral decryption/finalization by a single actor* → finalization is only reachable once **both** a configured number of independent governance approvals **and** a configured number of independent trustee key-shares have been recorded — no single call can finalize a ceremony on its own.
- *Unauthorized ceremony manipulation* → non-health endpoints require operator-level access, and every governance/share action is recorded as a ceremony event for later audit.

**Architectural note on scope:** the ceremony's role is to *govern the release* of a final tally, and to bind that release to a deterministic, reproducible digest — not to mandate a specific cryptographic threshold-decryption scheme. Any concrete threshold cryptosystem (e.g., threshold ElGamal, Shamir-based share combination) can be plugged into the "trustee share" and "finalize" steps without changing the governance state machine described here.

---

## 4. Smart Contract and Blockchain Interactions

### 4.1 Contract Model

- A single, **reusable, generic election program** is deployed once (at the framework level) and *initialized* — not redeployed — for each new election, with election-specific parameters (schedule, candidate set, cryptographic policy references) passed at initialization time.
- No per-election dynamic contract-code generation is permitted. This keeps the verifiable on-chain surface constant across every election run by the framework, which is a precondition for the Watcher/Transparency components being able to reason about "canonical state" generically instead of per-election bespoke logic.

### 4.2 Election Contract Lifecycle (On-Chain View)

| Step | Actor | On-chain effect |
| --- | --- | --- |
| 1. Initialize | Election Configuration Component | Election program instance created with election parameters; contract reference returned. |
| 2. Accept ballots | Election Processing Component | Batch anchoring transactions accepted while the election is Open. |
| 3. Close | (time-based / operator-triggered) | Election transitions to Closed; no further batch anchoring accepted. |
| 4. Finalize | Threshold Decryption Orchestration Component (via its governance ceremony) | Final tally reference associated with the election; election reaches Finalized. |

### 4.3 Blockchain Interaction Catalogue

| Interaction | Initiator | Payload | Purpose |
| --- | --- | --- | --- |
| **Initialize election program** | Election Configuration Component | Election parameters, metadata hash/URI | Bind a reusable program instance to one election. |
| **Anchor batch** | Election Processing Component | Batch root (Merkle root over ballot commitments), proof reference, batch metadata (election id, batch id/sequence, size) | Canonical settlement of one processed ballot batch. |
| **Read canonical election/settlement state** | Transparency & Audit Component, Watcher Component | Election id | Retrieve authoritative lifecycle/settlement state to reconcile off-chain projections. |
| **Confirm election closed** | Threshold Decryption Orchestration Component | Election id | Precondition check before a decryption ceremony may be proposed. |
| **Associate final tally reference** | Threshold Decryption Orchestration Component (via publication step) | Result digest, publication reference | Make the finalized result independently checkable against a canonical reference. |

### 4.4 Cryptographic and Privacy Interactions Tied to the Ledger

- **Ballot confidentiality:** the ledger never receives ballot plaintext — only ciphertext-derived commitments and batch roots. Confidentiality is enforced entirely off-chain, before anchoring.
- **Batching integrity / inclusion proofs:** the Merkle root anchored on-chain is the only thing the ledger needs to store per batch; individual-ballot inclusion is provable off-chain against that root via `InclusionProofReference` (§2.4.3), without the ledger storing every ballot individually.
- **Post-close decryption:** the ledger's role is limited to (a) authoritatively marking the election Closed, and (b) later carrying/confirming the reference to the finalized tally publication — the decryption computation itself is a governance-gated, off-chain-attested process (§3.11), not an on-chain computation.
- **Proof system independence:** the architecture deliberately does not freeze a single proof system at the ledger-interaction level; any proof system that can produce a verifiable artifact referenceable from an anchoring transaction satisfies the contract.

---

## 5. Trust and Data Authority Model

### 5.1 Authority Hierarchy

1. Canonical Settlement Ledger records and finalized election state.
2. Election Metadata Repository entries tied to an initialized election program reference.
3. Transparency & Audit Component's verified projections, built from (1) and (2).
4. Result Portal and Watcher presentation/report views.

### 5.2 Off-Chain Data Policy

Off-chain stores are query accelerators and evidence projections. They are never allowed to diverge from, or override, canonical ledger truth.

### 5.3 Conflict Resolution Rule

If off-chain and on-chain data conflict, canonical on-chain settlement state always prevails; the off-chain projection is marked stale/inconsistent and reconciled (reindexed or formally disputed), never silently overwritten.

### 5.4 User Trust Requirement

When a voter queries a vote by VID, the framework must provide: a settlement reference, a batch identifier, an inclusion/proof reference, and a verification outcome tied to canonical ledger state (§2.4.4).

---

## 6. Cryptographic and Privacy Model

### 6.1 Identity Separation
Real identity is handled only at the TTP/Election Authorization boundary. The VID is the privacy-preserving identity surrogate for every subsequent election operation.

### 6.2 Ballot Confidentiality
Ballots are encrypted before ever leaving the Voter Component. The Election Processing Component only ever handles encrypted payloads, commitments and nullifiers.

### 6.3 Batching Integrity
A Merkle tree is built over ballot commitments per batch; a proof is generated (and verified before anchoring) for batching correctness and duplicate/nullifier checks (§2.4.2).

### 6.4 Post-Close Decryption
Threshold decryption is gated behind a governance ceremony requiring independent approvals and independent trustee shares (§3.11); no single actor can unilaterally decrypt or finalize a result before close.

### 6.5 Public Verifiability
Every result/verification view carries references that map back to canonical settlement records (§2.4.4), so "trust me" claims are never the only evidence offered to an end user.

---

## 7. Consistency Guarantees

### 7.1 Portal Consistency Guarantee
Any vote/result shown in the Result Portal must be traceable to a canonical ledger settlement record, a matching metadata context, and a verified projection from the Transparency & Audit Component.

### 7.2 Audit Consistency Guarantee
The Watcher Component cross-validates metadata definitions, audit evidence, transparency projections, and canonical ledger settlement/finalization state.

### 7.3 Conflict Resolution
See §5.3.

---

## 8. Failure Handling Model

| Failure class | Architectural response |
| --- | --- |
| **Authentication/authorization failure** | Access denied; no VID session is established for election interaction. |
| **Submission failure (broker unavailable)** | The Voter Component retains the ballot durably and retries; the voting grant is only consumed once durable submission succeeds, never before. |
| **Processing/proof failure** | Only the affected election is blocked for retry; other elections continue processing. No partial settlement is treated as valid without ledger confirmation. |
| **Ledger settlement failure** | The batch remains pending, never "settled"; diagnostics are recorded in the Audit Repository. |
| **Read-model drift** | The Transparency & Audit Component re-syncs/reindexes from canonical ledger + audit sources; drift is surfaced to the Watcher Component rather than hidden. |

---

## 9. Security and Access Model

### 9.1 Public vs Private Election Access
Result Portal access policy depends on election visibility mode: public elections may allow anonymous reads; private elections enforce a separate portal-level authorization check before any Transparency query is made.

### 9.2 Watcher Access
Target posture: strict, role-restricted access — the Watcher role is a trusted auditing entity, not a public one.

### 9.3 Threshold Decryption Access
Target posture: strict, role-restricted access for governance approvers and trustees; workflow-level actor identities should be backed by strong identity controls.

### 9.4 Least Privilege
Every component only holds the permissions required for its own responsibilities; write-path secrets and read-path services are kept separate.

### 9.5 Key Management Considerations
Encryption key lifecycle and threshold key-share governance must prevent unilateral decryption before close, and must define custody/rotation responsibility clearly at the operational level.

---

## 10. Non-Functional Targets (Architecture-Level)

- Deterministic election provisioning and immutable metadata after deployment.
- Horizontal scalability for message-broker consumers and batch processing.
- High audit traceability across all inter-component operations.
- Eventual consistency with explicit canonical-source reconciliation (never silent divergence).
- Clear observability for operations, failures, and drift detection.

---

## 11. Design Decisions Locked

1. A single, reusable smart-contract program is initialized per election; no per-election dynamic contract generation.
2. The canonical settlement ledger is the sole canonical authority for settlement truth.
3. No component may present raw off-chain vote data as trustworthy without a canonical reference.
4. The Transparency & Audit Component is the only verifiable read-model layer; the Result Portal and Watcher never bypass it.
5. Election Processing anchors batch root + proof reference + settlement metadata as one atomic evidentiary unit.
6. Threshold decryption occurs only after election close, gated by independent governance approvals and trustee shares.
7. The Result Portal always surfaces verification references, never opaque, unreferenced claims.

## 12. Open Items for Future Architectural Evolution

These are explicitly **architecture-level** open questions — independent of any one implementation's current status (see [Prototype.md](Prototype.md) for the prototype's concrete position on each):

1. Exact on-chain account schema for batch and tally state (a stable, versioned account layout).
2. A frozen proof format and verification pathway (on-chain vs. off-chain-referenced verification).
3. A fully specified threshold-decryption cryptographic ceremony (beyond the governance state machine described in §3.11).
4. Data retention policy for encrypted ballots and audit artifacts.
5. A formally published, versioned public API contract for the Transparency & Audit Component.

---

## 13. Appendix — Diagram Index

| Diagram | Depicts |
| --- | --- |
| [`DIAGRAMS/HL_LogicView.svg`](DIAGRAMS/HL_LogicView.svg) | Full logical component view and inter-component data flows. |
| [`DIAGRAMS/HL_Deployment.svg`](DIAGRAMS/HL_Deployment.svg) | High-level deployment topology and scaling strategy (HPA baseline, KEDA stress mode). |
| [`DIAGRAMS/HL UIS-01.svg`](DIAGRAMS/HL%20UIS-01.svg) | UIS-01 — Voter Authentication and Voting Authorization. |
| [`DIAGRAMS/HL UIS-02.svg`](DIAGRAMS/HL%20UIS-02.svg) | UIS-02 — Ballot Casting and Vote Submission (including batching, proof verification and evidence publication). |
| [`DIAGRAMS/HL UIS-03.svg`](DIAGRAMS/HL%20UIS-03.svg) | UIS-03 — Individual Vote Verification by VID. |
| [`DIAGRAMS/HL UIS-04.svg`](DIAGRAMS/HL%20UIS-04.svg) | UIS-04 — Election Configuration and Initialization. |
| [`DIAGRAMS/HL UIS-05.svg`](DIAGRAMS/HL%20UIS-05.svg) | UIS-05 — Election Auditing (Watcher). |
| [`DIAGRAMS/HL UIS-06.svg`](DIAGRAMS/HL%20UIS-06.svg) | UIS-06 — Election Closing, Threshold Decryption ceremony, and Result Publication. |
| `DIAGRAMS/LL * - Sequence.svg` | Internal operation sequences for each core component. |

All diagrams are published as static SVG figures; no editable diagram source is retained in this repository.

---

*For the concrete, running instance of this architecture — technology choices, component-instance naming map, scope boundaries, and full validation/test evidence — see [Prototype.md](Prototype.md).*
