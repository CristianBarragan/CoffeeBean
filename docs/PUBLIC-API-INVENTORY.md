# Public API Inventory — Phase 12

The intended public concepts are:

- semantic model
- capability contract
- semantic intent
- authorization context/decision
- semantic plan
- plan optimization
- dry-run inspection
- plan approval
- execution
- execution receipt
- provider execution contracts
- transport adapters

## Compatibility rule

Existing APIs should be retained where practical and marked for migration only after
the consolidated architecture has been compiled and integration-tested.

## Review targets

During the first real .NET build, identify:

- duplicate capability types
- duplicate plan types
- duplicate mutation request types
- duplicate evidence/receipt types
- adapter types leaking into core namespaces
- public APIs that bypass authorization
