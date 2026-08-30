# Foundgine.GraphQL.HotChocolate

`Foundgine.GraphQL.HotChocolate` is the thin query-side GraphQL adapter for Foundgine.

It translates Hot Chocolate GraphQL syntax into provider-independent Foundgine semantic requests and shapes Foundgine results back into GraphQL responses.

## Boundary

```text
GraphQL request
      ↓
HotChocolateSemanticAdapter
      ↓
SemanticRequest
      ↓
Foundgine
      ↓
provider
      ↓
GraphQL result shape
```

This package handles GraphQL syntax. It does not own semantic planning or physical execution.

## What it translates

The adapter handles GraphQL concepts including:

- selection sets;
- fields;
- nested relationships;
- aliases;
- variables;
- fragments;
- directives;
- operation selection;
- schema description;
- result shape.

The resulting semantic request is independent of Hot Chocolate.

## Security boundary

`HotChocolateSemanticAdapter` is a translator, not an authentication or authorization boundary.

GraphQL arguments/variables must not supply:

- identity;
- tenant;
- audience;
- warrant;
- provider credentials.

The host establishes security context before execution.

For example:

```text
ASP.NET authentication
        ↓
host security context
        ↓
GraphQL adapter
        ↓
SemanticRequest
        ↓
Foundgine authorization
```

## Secure execution package

For the standard secure query execution path, use:

`Foundgine.GraphQL.HotChocolate.Execution`

Its `FoundgineHotChocolateQueryExecutor` requires a host-supplied `ISecurityExecutionContextProvider` and routes execution through the Foundgine runtime.

This separates:

```text
GraphQL translation
```

from:

```text
secure Foundgine execution
```

so an application does not accidentally treat a syntax adapter as an execution gate.

## Query semantics

GraphQL selection names are translated into semantic field/relationship names.

The semantic model remains authoritative.

If a GraphQL field is not exposed by the semantic model, GraphQL translation does not create it.

Likewise, a GraphQL traversal cannot bypass an intermediate semantic relationship or authorization rule.

## Schema description

`GraphQLSchemaAdapter` and `GraphQLSchemaDescriptor` can describe the semantic surface as GraphQL metadata.

The generated schema should be understood as an interface over the semantic model, not as the source of application authorization.

## Result shaping

The adapter maps provider-independent result shapes into GraphQL field/alias structure.

Physical provider rows do not leak into GraphQL.

## Errors

`GraphQLAdapterError` and related result types allow syntax/translation errors to remain distinct from downstream semantic, authorization, and provider errors.

Do not hide authorization failures by turning them into successful empty responses.

## What this package does not do

It does not:

- generate SQL;
- execute database commands;
- define semantic authorization;
- authenticate callers;
- host ASP.NET;
- call an LLM;
- implement MCP.

## Mutations

Query and mutation GraphQL support are intentionally separated.

Use:

- `Foundgine.GraphQL.HotChocolate.Mutations` for mutation translation;
- `Foundgine.GraphQL.HotChocolate.MutationExecution` for secure mutation execution.

## Related packages

- `Foundgine` — runtime.
- `Foundgine.Semantics` — semantic request model.
- `Foundgine.GraphQL.HotChocolate.Execution` — secure query execution.
- `Foundgine.GraphQL.HotChocolate.Mutations` — mutation adapter.

## Target framework

- .NET 9
- Hot Chocolate Language APIs
- MIT licensed
