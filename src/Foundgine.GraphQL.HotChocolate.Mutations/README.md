# Foundgine.GraphQL.HotChocolate.Mutations

GraphQL mutation adapter.

It translates GraphQL mutation input and result selections into Foundgine mutation intents and result shapes. Planning and execution remain provider-neutral.

## Secure GraphQL mutation execution

Use `Foundgine.GraphQL.HotChocolate.MutationExecution` and `FoundgineHotChocolateMutationExecutor` for mutation execution. The executor obtains the host-owned `ISecurityExecutionContextProvider`, converts the GraphQL nested intent into the canonical `SemanticMutationOperationGraph`, and routes it through `IFoundgineMutations.ExecuteAsync`. This ensures warrant validation, tenant/audience/resource-scope checks, semantic authorization, security-invariant certification, replay protection, and provider execution all use the same mutation security boundary.

`HotChocolateMutationAdapter` remains a pure translation component. It does not establish identity or authorization and should not be treated as an execution boundary.
