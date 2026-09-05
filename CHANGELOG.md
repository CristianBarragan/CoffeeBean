# Changelog

## 2.0.2 — September 6, 2026

### Semantic grounding

- Fixed `SemanticLexicalResolver.Ground` so the compact-name fallback (e.g. `customer profile` → `customerprofile`, `purchase order` → `purchaseorder`) is actually reached: the initial per-token retrieval pass now stops as soon as any single token comes back with no candidates, instead of continuing to query the remaining tokens first. Continuing to query every token before falling back was spending the shared retrieval-timeout budget on results that were about to be discarded anyway, which could starve the compact-fallback lookup of the time it needed and made it appear as though the fallback simply hadn't run.
- Fixed a latent `KeyNotFoundException` in the "no lexical candidate" diagnostic: if the compact-name fallback itself also returned no candidates, the resolver could try to look up a token that had never been queried (because retrieval stopped early per the fix above) and crash instead of reporting `GroundingOutcome.Unresolved` cleanly.

### Tests

- Widened the timing margins in `Ground_uses_one_retrieval_deadline_across_compact_token_fallback` (both `Foundgine.Semantics.Tests` and the `Foundgine.SupplyChain.Advanced` sample) so the assertions no longer depend on sub-15ms real-time scheduling precision. Windows' default thread-timer resolution can silently round a `Thread.Sleep(1)` up to ~15ms, which was enough on its own to exhaust the previous 10ms retrieval budget before the compact-token lookup even started. The test now uses a `TimeSpan.Zero` sleep for the always-fast lookup and a 200ms budget with a 500ms fallback delay, comfortably clear of any plausible scheduler jitter, while asserting exactly the same behavior.

### Release

- Version: `2.0.2`
- Target framework: `.NET 9`
- License: MIT

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
