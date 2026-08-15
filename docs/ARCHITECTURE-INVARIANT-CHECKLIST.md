# Architecture Invariant Checklist

Before merging the next architectural feature:

- [ ] Core semantics compile without MCP/GraphQL/EF provider references.
- [ ] There is one canonical capability contract.
- [ ] There is one canonical semantic plan identity.
- [ ] There is one approval contract.
- [ ] There is one canonical ExecutionReceipt.
- [ ] MCP translates to Foundgine intent/capabilities and does not execute providers directly.
- [ ] Authorization is evaluated before optimization and provider execution.
- [ ] Optimizers cannot grant access or remove required policy constraints.
- [ ] Approved execution re-authorizes and verifies the exact plan fingerprint.
- [ ] AOT artifacts do not encode request-specific authorization decisions.
- [ ] Security/fuzz tests exercise the same production semantic path.
