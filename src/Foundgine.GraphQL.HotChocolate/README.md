# Foundgine.GraphQL.HotChocolate

Thin Hot Chocolate adapter for query-side GraphQL.

```text
GraphQL → AST → Semantic Request → Foundgine runtime
```

It handles GraphQL syntax, variables, fragments, aliases, directives, operation selection, schema description, and response shape. It does not perform planning, SQL, or execution.

## Security

`HotChocolateSemanticAdapter` is a pure translator: it has no opinion about the
caller's identity and never reads security material from the GraphQL request
payload. GraphQL requests can never supply identity, tenant, audience, or
warrant context — that is always established by the host, from its own
authentication mechanism (for example `HttpContext.User`), before a request
reaches Foundgine.

If you wire execution yourself, you are responsible for attaching a
`SecurityExecutionContext` to the translated `SemanticRequest` before calling
`IFoundgine.ExecuteAsync`. Foundgine's engine fails closed (rejects the
request) if no warrant-backed context is required by your authorization
policy, but nothing stops a host from forgetting to attach one when a policy
does allow anonymous requests.

For a secure execution path, use the separate `Foundgine.GraphQL.HotChocolate.Execution` package. It keeps this adapter independent of the execution runtime while providing `FoundgineHotChocolateQueryExecutor`, which requires a host-supplied `ISecurityExecutionContextProvider`.

**Mutations:** see the security note in
`Foundgine.GraphQL.HotChocolate.Mutations`'s README — GraphQL mutations do
not currently have an equivalent secured execution path.
