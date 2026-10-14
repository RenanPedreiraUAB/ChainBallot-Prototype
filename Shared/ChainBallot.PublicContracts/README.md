# ChainBallot.PublicContracts

This project hosts shared, public data contracts used across Chain Ballot components.

Purpose:

- Keep settlement and audit handoff models in a single, reviewable location.
- Avoid duplicated DTOs and conflicting assumptions between components.
- Make trust boundaries explicit for implementation validation.

Current contract groups:

- `BallotSubmissionPackage`: queue handoff envelope for outbound processing.
- `EncryptedBallotPayload`: encrypted vote payload contract produced by inbound.
- `QueueReceipt`: queue publish acknowledgment contract for submission UX.
- `SettlementReference`: canonical chain reference metadata.
- `InclusionProofReference`: inclusion proof pointer and leaf linkage.
- `BatchAnchorReference`: outbound anchoring handoff contract.
- `VidVerificationResponse`: result/verification API response contract.
- `VerifiedResultSnapshot`: verified read-model snapshot contract.

This project intentionally contains contracts only (no infrastructure concerns).
