# Execution Evidence

Foundgine execution returns provider-neutral evidence when the provider supplies it.

The evidence answers four operational questions without retaining raw request objects or provider runtime state:

1. **What intent was submitted?** — `IntentFingerprint` identifies the semantic request without storing the request itself.
2. **What authorized execution shape was used?** — `PlanFingerprint` identifies the provider plan shape, while `AuthorizationFingerprint` fingerprints the complete authorized logical plan.
3. **Where did execution occur?** — `Provider` identifies the execution provider.
4. **What happened?** — `RowsReturned`, elapsed time, and an optional hashed provider-operation fingerprint describe the execution outcome.

## Evidence is not authorization

Evidence is diagnostic/provenance data. It does not grant access and is never used as an authorization cache. Authorization happens before planning and provider compilation.

## Privacy boundary

Evidence stores fingerprints rather than the original semantic request, authorization expressions, SQL text, or provider runtime objects. Provider-specific operation fingerprints are hashes as well.

## Canonical flow

```text
Intent
  ↓
Semantic resolution
  ↓
Authorization
  ↓
Execution plan
  ↓
Provider
  ↓
Execution evidence
```
