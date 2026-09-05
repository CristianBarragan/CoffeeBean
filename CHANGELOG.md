# Changelog

## 2.0.1 — September 5, 2026

### Semantic grounding

- Fixed retrieval timeout accounting so one retrieval deadline is shared across all tokens and the compact-name fallback; provider overruns are detected after synchronous retrieval returns, and cancellable providers receive a linked timeout token.
- Corrected `candidateLimit` documentation to describe the implemented total per-token limit across semantic kinds.
- Renamed `GroundingInterpretation.Confidence` to `InterpretationScore` to make its heuristic ranking semantics explicit.
- Clarified that ambiguity is based on score separation within the configured ambiguity margin rather than a literal tie.
- Clarified that alias `Weight` is application-declared evidence weight, not confidence or priority.
- Documented that `CompetingInterpretations` is internal semantic metadata and should be projected safely before exposure to untrusted callers.
- Corrected the advanced Supply Chain weighted-alias test so it asserts only the semantic identity that actually participated in lexical grounding.

- Restored deterministic alias/synonym grounding for generated semantic contracts.
- Canonical semantic identity is independent of caller wording, so a declared alias resolves to the same interpretation as its canonical name.
- Retrieval representations of the same stable semantic identity no longer create false ambiguity during grounding.
- Added a bounded compact-name fallback for multi-word canonical names such as `purchase order` when token-by-token lexical retrieval does not produce a candidate.
- Advanced Supply Chain grounding coverage explicitly exercises the README case:
  - `purchase orders` / `buys` → `PurchaseOrder`
  - `supplier` / `seller` → `Supplier`
- Added optional alias evidence weights (1–100) for entity, field, and relationship aliases, with `AliasWeightEvidenceGate` for thresholded, fail-closed evidence evaluation that never grants authorization.

### Documentation

- Updated the root README to explain the canonical request and its alias-matched paraphrase layer by layer.
- Added a concrete PlantUML source and matching SVG for the alias-matched Supply Chain execution path under `docs/assets/`.
- Documented that alias matching changes vocabulary, not authority: retrieval remains discovery, while authorization remains application-controlled.

### Release

- Version: `2.0.1`
- Target framework: `.NET 9`
- License: MIT
