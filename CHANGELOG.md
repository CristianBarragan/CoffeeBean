# Changelog

## 2.0.1 — September 5, 2026

### Semantic grounding

- Restored deterministic alias/synonym grounding for generated semantic contracts.
- Canonical semantic identity is independent of caller wording, so a declared alias resolves to the same interpretation as its canonical name.
- Retrieval representations of the same stable semantic identity no longer create false ambiguity during grounding.
- Added a bounded compact-name fallback for multi-word canonical names such as `purchase order` when token-by-token lexical retrieval does not produce a candidate.
- Advanced Supply Chain grounding coverage explicitly exercises the README case:
  - `purchase orders` / `buys` → `PurchaseOrder`
  - `supplier` / `seller` → `Supplier`

### Documentation

- Updated the root README to explain the canonical request and its alias-matched paraphrase layer by layer.
- Added a concrete PlantUML source and matching SVG for the alias-matched Supply Chain execution path under `docs/assets/`.
- Documented that alias matching changes vocabulary, not authority: retrieval remains discovery, while authorization remains application-controlled.

### Release

- Version: `2.0.1`
- Target framework: `.NET 9`
- License: MIT
