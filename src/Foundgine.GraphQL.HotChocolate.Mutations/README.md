# Foundgine.GraphQL.HotChocolate.Mutations

GraphQL mutation adapter.

It translates GraphQL mutation input and result selections into Foundgine mutation intents and result shapes. Planning and execution remain provider-neutral.

## Known limitation: no warrant-backed security path for mutations

`HotChocolateMutationAdapter` translates GraphQL mutations into a
`NestedMutationIntent`, which flows through `Foundgine.Planning.Mutation`'s
structural planner into a `MutationBatchPlan`. `MutationAuthorizer` does apply
entity/field-level write authorization to that plan via your
`ISemanticAuthorizationPolicy`. However, `NestedMutationIntent` has no
`Security` field, and this pipeline never verifies a `SecurityExecutionContext`
warrant — that verification exists only in `FoundgineMutationEngine`
(`IFoundgineMutations`), which operates on a different, unrelated type
(`SemanticMutationOperationGraph`) that nothing currently converts
`NestedMutationIntent` into.

In practice this means: **if your authorization policy relies on
warrant-backed `SecurityExecutionContext` checks (subject, tenant, audience,
resource scope) rather than solely on entity/field policy, those checks are
not enforced for GraphQL mutations today**, even though they are enforced for
GraphQL queries (via `IFoundgine.ExecuteAsync(SemanticRequest, ...)`) and for
MCP mutations (via `Foundgine.MCP.FoundgineMcpMutationTools`).

If your policy is expressed entirely through
`ISemanticAuthorizationPolicy.GetEntityAccess`/`GetFieldAccess`/etc. with no
warrant dependency, `MutationAuthorizer` already protects you. If you need
warrant-backed checks on mutations, you currently need to either enforce them
yourself around this adapter's output, or wait for one of:

- a converter from `NestedMutationIntent` to `SemanticMutationOperationGraph`
  so GraphQL mutations can route through `IFoundgineMutations`, or
- warrant verification wired directly into the
  `Foundgine.Planning.Mutation.MutationPlanner` / `MutationBatchPlan` path.

Neither exists yet; this is a tracked gap, not a silent one.
