# Security

Foundgine treats external intent as untrusted input.

The fundamental rule is:

```text
Input
  ↓
Parse
  ↓
Resolve
  ↓
Validate
  ↓
Authorize
  ↓
Plan
  ↓
Provider security conformance
  ↓
Execute
```

No transport adapter should bypass the semantic/security boundary.

## Authority and intent are different

The caller controls intent:

```text
"read Customer.Name"
```

The trusted host controls authority:

```text
identity
tenant
audience
warrant
```

A request must never be able to promote itself by changing ordinary intent fields.

## Semantic authorization

Authorization is evaluated at the semantic boundary for:

- entity read/write access;
- field read/write access;
- relationship read/write access;
- conditional resource predicates;
- mutation operations and returned fields.

Capability discovery is advisory. The policy is evaluated again for the actual request.

## Conditional authorization

A policy can carry a provider-independent predicate such as:

```text
resource.TenantId == context.TenantId
```

The predicate survives into the logical plan and is lowered by the provider.

This is important for:

- tenant isolation;
- user/resource ownership;
- row-level access;
- future safe plan caching.

A provider must not discard a predicate merely because the provider can produce a syntactically valid query without it.

## Logical traversals

A logical traversal can hide intermediate entities:

```text
Customer
  → CustomerRelationship
  → Contract
  → Transaction
```

Authorization sees the expanded path.

Therefore:

```text
deny Contract
    ⇒ deny Customer.transactions
```

A traversal is not a security shortcut.

## Capability discovery

Capability discovery exists so dynamic callers and AI agents can construct valid intent.

```text
capability description
        ↓
caller constructs intent
        ↓
Foundgine resolves
        ↓
authorization
        ↓
execution
```

A capability document is not an authorization token.

## Fail-closed projection

An especially important invariant is that an empty authorized field set must not mean "select everything".

The execution/provider boundary must reject an invalid empty projection rather than widening the selection.

## Provider security conformance

A provider can execute valid SQL and still violate Foundgine's security contract.

Foundgine therefore carries required security invariants into execution and checks provider conformance.

```text
plan requirements
      ↓
provider guarantees
      ↓
conformance
   ├── satisfied → execute
   └── missing   → reject
```

## GraphQL

GraphQL input is untrusted.

Do not accept identity, tenant, audience, warrant, or provider control information from GraphQL variables/arguments as trusted authority.

Use:

`Foundgine.GraphQL.HotChocolate.Execution`

for the secure query execution path.

For mutations use:

`Foundgine.GraphQL.HotChocolate.MutationExecution`.

## MCP

MCP tool arguments are untrusted.

The MCP host should obtain security context from authenticated session/request state.

```text
MCP request
    ↓
host authentication
    ↓
ISecurityExecutionContextProvider
    ↓
Foundgine
```

Do not allow an agent to select its own tenant or authorization role.

## AI

An LLM is an untrusted producer of intent.

Avoid:

```text
LLM → SQL → database credentials
```

Use:

```text
LLM
 ↓
semantic tool
 ↓
Foundgine
 ↓
authorization
 ↓
provider
```

The application remains responsible for authentication, model credentials, rate limits, quotas, and prompt/application policy.

## JSON

JSON intent should be bounded with `JsonReadIntentAdapterOptions`.

Structural limits protect the parser/intent boundary, while semantic authorization protects the operation.

## Mutations

Writes have stronger security requirements than reads.

The mutation path can include:

```text
semantic authorization
      ↓
security invariants
      ↓
approval / plan binding where configured
      ↓
revalidation
      ↓
provider execution
```

A mutation builder is an authoring tool, not an authorization mechanism.

## Plan caching

Caching must never cache away authorization.

The safe conceptual model is:

```text
current request
    ↓
resolve
    ↓
authorize
    ↓
reuse/compile plan shape
    ↓
bind current runtime context
    ↓
execute
```

## Authority recovery

`Foundgine.Security.Authority` is optional.

It provides authority/control-plane recovery primitives such as witness quorum, credential lifecycle, journal reconciliation, promotion/failover, and recovery evidence.

It is not required for normal semantic authorization and remains outside the core execution path.

## Application responsibilities

Foundgine does not replace application security infrastructure.

The application/host remains responsible for:

- authentication;
- identity lifecycle;
- tenant resolution;
- secret management;
- network security;
- rate limits;
- quotas;
- endpoint/session policy;
- external identity provider integration;
- authorization policy administration.

Foundgine enforces the semantic execution boundary supplied by the application.

## Security testing rule

Every new transport/provider should have adversarial tests proving that it cannot:

1. bypass semantic resolution;
2. bypass authorization;
3. replace host-owned authority;
4. drop a required security predicate;
5. execute a plan without required provider security conformance.
