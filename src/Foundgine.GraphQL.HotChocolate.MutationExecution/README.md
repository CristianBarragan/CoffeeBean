# Foundgine.GraphQL.HotChocolate.MutationExecution

Secure Hot Chocolate GraphQL mutation execution for Foundgine.

`FoundgineHotChocolateMutationExecutor` treats GraphQL as an untrusted transport. It obtains the caller security context only from the host-owned `ISecurityExecutionContextProvider`, converts the GraphQL mutation into the canonical `SemanticMutationOperationGraph`, and routes execution through `IFoundgineMutations.ExecuteAsync`.

This guarantees that GraphQL mutations use the same warrant validation, tenant/audience/resource-scope checks, semantic authorization, security-invariant certification, replay protection, and provider execution boundary as other Foundgine mutation transports.
