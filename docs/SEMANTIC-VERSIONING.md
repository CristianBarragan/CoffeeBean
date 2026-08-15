# Semantic Versioning

Foundgine treats semantic compatibility as part of the execution boundary.
External agents and adapters must not assume that a capability, intent, plan,
or approval remains valid when the semantic application model changes.

## Version set

Every engine exposes a `SemanticVersionSet` containing:

- `SemanticModelVersion` — a deterministic SHA-256 identity of the semantic topology.
- `CapabilityContractVersion` — version of the machine-readable capability contract schema.
- `CapabilityVersion` — compatibility version of capability definitions.
- `IntentVersion` — compatibility version of the canonical semantic request protocol.
- `PlanVersion` — compatibility version of the semantic plan representation.

The semantic model version changes when the canonical model topology changes. The
other values change when their corresponding contracts evolve incompatibly.

## Approval invariant

A `PlanApproval` captures the complete version set at approval time. Execution
rejects an approval when any version no longer matches the current engine.

Therefore:

```text
Dry Run
  ↓
Authorized Plan
  ↓
Version Set + Plan Fingerprint
  ↓
Approval
  ↓
Current Version Set Check
  ↓
Re-authorize + Re-plan
  ↓
Fingerprint Check
  ↓
Execute
```

An approval is never a permanent authorization grant.

## Receipt invariant

`ExecutionReceipt` records the semantic model and contract versions that were
used for the execution. This makes a receipt interpretable after the application
has evolved and allows audit systems to distinguish executions performed under
different semantic contracts.

## Fingerprints

Intent fingerprints are bound to the intent protocol version. Semantic plan
fingerprints are bound to the plan representation version. The semantic model
version is carried separately because it identifies the model topology rather
than the shape of one individual request.

This separation is intentional:

```text
Model version       = which application semantics existed
Intent version      = how the request was represented
Plan version        = how the semantic plan was represented
Capability version  = how capability meaning was represented
Fingerprint         = exact artifact/content identity
```

## Compatibility rule

Never silently migrate an externally approved plan across incompatible semantic
versions. Require a new dry-run and a new approval.
