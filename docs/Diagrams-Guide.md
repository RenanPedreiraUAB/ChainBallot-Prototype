# Chain Ballot Diagram Guide

This guide indexes the architecture and sequence diagrams maintained under `DIAGRAMS/`, and their editable sources under `DIAGRAMS_CODE/`.

## 1. Diagram Catalogue

Each diagram is published as a static SVG figure under `DIAGRAMS/` for direct viewing/embedding, with an editable source file of the same name under `DIAGRAMS_CODE/` (Mermaid `.mmd` for the HL logic/deployment views and LL architecture diagrams, WebSequenceDiagrams-style `.txt` for the UIS and LL sequence diagrams). When a diagram changes, update the source under `DIAGRAMS_CODE/` first, then re-render and replace the corresponding SVG under `DIAGRAMS/`.

High-level user interaction scenario (UIS) diagrams:
- `DIAGRAMS/HL UIS-01.svg` — Voter Authentication and Voting Authorization
- `DIAGRAMS/HL UIS-02.svg` — Ballot Casting and Vote Submission
- `DIAGRAMS/HL UIS-03.svg` — Individual Vote Verification
- `DIAGRAMS/HL UIS-04.svg` — Election Configuration and Initialization
- `DIAGRAMS/HL UIS-05.svg` — Election Auditing
- `DIAGRAMS/HL UIS-06.svg` — Election Closing, Threshold Decryption and Result Publication

Logical and deployment diagrams:
- `DIAGRAMS/HL_LogicView.svg` — full logical component view and inter-component data flows
- `DIAGRAMS/HL_Deployment.svg` — deployment topology and scaling strategy (HPA baseline, KEDA stress mode)

Low-level component sequence diagrams:
- `DIAGRAMS/LL Authorization - Sequence.svg`
- `DIAGRAMS/LL Configuration - Sequence.svg`
- `DIAGRAMS/LL Decryption - Sequence.svg`
- `DIAGRAMS/LL Processing - Sequence.svg`
- `DIAGRAMS/LL Result - Sequence.svg`
- `DIAGRAMS/LL Transparency - Sequence.svg`
- `DIAGRAMS/LL TTP - Sequence.svg`
- `DIAGRAMS/LL Voter - Sequence.svg`
- `DIAGRAMS/LL Watcher - Sequence.svg`

Architecture reference: `Architecture.md` section 2.2 and the Diagram Index appendix.

## 2. Consistency Rules

- Participant/role labels in every diagram must align with the architectural role names in `Architecture.md` section 2.1.
- Prototype-specific instance names (for example CynetAuth) are called out explicitly where a diagram depicts the concrete prototype rather than the abstract role.
- Endpoint/route labels shown in a diagram must match the routes documented in `docs/API-Reference.md`.
- Any behavior shown in a diagram that differs from the current prototype implementation is documented in `Architecture.md` / `Prototype.md` text, not as an in-diagram annotation.

## 3. Updating a Diagram

1. Edit the source file under `DIAGRAMS_CODE/` (same base filename as the SVG, `.mmd` or `.txt` depending on diagram type).
2. Confirm the new diagram matches the current endpoint routes and runtime behavior.
3. Re-render and replace the corresponding SVG file under `DIAGRAMS/` with the same filename.
4. Update `Architecture.md` and `docs/Traceability-Matrix.md` if the change affects section references.
