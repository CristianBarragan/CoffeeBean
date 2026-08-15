# Plan-Bound Approval

Foundgine approvals bind human or external approval to the exact authorized semantic plan that was inspected.

## Flow

```text
SemanticRequest
    ↓
Resolve
    ↓
Authorize
    ↓
SemanticPlan
    ↓
PlanFingerprint
    ↓
ApprovePlan()
    ↓
ExecuteApprovedAsync()
```

`ExecuteApprovedAsync` resolves and authorizes the request again. It refuses execution when the resulting plan fingerprint differs from the approved fingerprint.

This is intentionally stronger than approving an intent alone: changes to authorization policy, semantic resolution, selected fields, filters, relationships, or plan structure invalidate the approval.

## Security boundary

`PlanApproval` is not an authorization grant. Authorization is always evaluated again at execution time. Approval only establishes that the caller approved a particular authorized plan.

A future signed approval/receipt layer can add cryptographic identity and tamper evidence without changing this execution boundary.

## Semantic version binding

Approvals also capture the semantic version set. An approval is rejected if the
semantic model, capability contract, intent protocol, or plan representation has
changed since approval. This prevents a previously approved plan from silently
crossing a semantic contract boundary.
