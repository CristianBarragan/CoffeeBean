# Step 6 — Authorization as part of the capability definition

Declarative authorization requirements are now first-class metadata on a
capability, alongside its semantic contract, constraints and effects.

```text
Capability
 ├── semantic contract
 ├── authorization requirements   ← this step
 │     ├── policy   (SemanticCapabilityPolicyRequirement)
 │     ├── tenant   (SemanticCapabilityTenantRequirement)
 │     ├── resource (SemanticCapabilityResourceRequirement)
 │     └── state    (SemanticCapabilityStateRequirement)
 ├── constraints
 ├── effects
 ├── implementation binding
 └── metadata
```

`SemanticCapabilityAuthorizationRequirement` is an abstract record with four
sealed cases (`Policy`, `Tenant`, `Resource`, `State`). Each requirement
describes *what execution-time authorization must establish* — it carries no
request-specific state and makes no decision. The existing runtime decision
(`AuthorizationDecision`, exposed as `SemanticCapabilityDefinition.Authorization`)
is unchanged and remains the only thing that can actually permit or deny a
call; the requirement list is descriptive metadata a caller/adapter can use to
know what authorization context it needs to establish before invoking a
capability.

`SemanticCapabilityMapping.ToDefinition(...)` gained an
`authorizationRequirements` parameter so the mapping layer can attach these
declaratively without touching application code, mirroring how `inputs`,
`constraints`, and `effects` are already supplied. Requirement order is
preserved end to end (mapping → `SemanticCapability` →
`SemanticCapabilityDefinition`), and each requirement type validates its own
required value (empty policy/tenant key/resource type/state is rejected at
construction).

Still intentionally **not** added: no adapter (Agent Framework, MCP, GraphQL)
yet reads `AuthorizationRequirements` to build its own projection. That
remains downstream — this step only makes the requirement metadata part of
the authoritative capability definition every adapter will eventually
consume.

## Hardening pass: string discriminators → enums

Two stringly-typed spots in the capability layer were tightened to match the
codebase's existing convention (`AuthorizationPredicateKind`,
`SemanticMutationEffectKind`, etc.) of using enums for closed discriminator
sets:

- **`SemanticCapabilityAuthorizationRequirement.Kind`** was a hand-typed
  `string` ("policy"/"tenant"/"resource"/"state") set independently in each
  subtype's constructor. It's now `SemanticCapabilityAuthorizationRequirementKind`,
  a 4-value enum, so a typo in the discriminator can no longer slip past the
  compiler. The four public constructors (`SemanticCapabilityPolicyRequirement`,
  etc.) are unchanged — only the internal discriminator's type changed.
- **`SemanticCapabilityContractDiscovery.BuildMutationActions`** iterated a
  raw `string[] { "create", "update", "delete", "upsert" }` through several
  `switch` expressions, each with a `_ => ...` catch-all. Adding a fifth
  action to the array without updating every switch would have silently
  produced an empty constraint/effect list instead of failing anything. It
  now iterates an internal `SemanticCapabilityWriteAction` enum with no
  catch-all arms, so the compiler (CS8509) flags any switch left unhandled.

`SemanticCapability.Operation` itself stays a `string` — it's an open,
application-defined identifier (existing capabilities in this codebase use
values like `"transferFunds"` and `"advance_fulfillment"`, not just CRUD
verbs), so it can't become a closed enum without breaking that
extensibility. Instead, a new `SemanticCapabilityOperations` static class
exposes `Read`/`Write`/`Create`/`Update`/`Delete`/`Upsert`/`Traverse` as
`const string` values, plus a `From(SemanticMutationKind)` mapping helper.
Every place in `Foundgine.Semantics.Capabilities` and the `Foundgine`
execution engine (`FoundgineEngine.cs`, `FoundgineMutationEngine.cs`) that
compared a capability's `Operation` against one of these built-in verbs now
goes through the shared constant instead of an independently hand-typed
literal — previously `"read"` was spelled out separately in three different
files, and `FoundgineMutationEngine` derived the mutation-verb string via
`operation.Kind.ToString().ToLowerInvariant()`, which would have silently
broken capability lookup if `SemanticMutationKind`'s member names ever
diverged from the operation strings.
