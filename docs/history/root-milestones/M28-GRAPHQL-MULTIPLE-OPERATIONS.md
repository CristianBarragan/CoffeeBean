# M28 — GraphQL Multiple Operations

M28 adds operation selection to the Hot Chocolate adapter.

## Supported

- Documents containing multiple named query operations.
- Documents containing multiple named mutation operations.
- Explicit `operationName` selection.
- Existing single-operation calls remain unchanged.
- Missing operation names for multi-operation documents fail explicitly.
- Unknown operation names return the existing GraphQL validation error shape through `TryAdapt`.
- Query and mutation result-shape adapters use the same operation selection rules.

## Boundary

Operation selection is a GraphQL transport concern and remains entirely inside `Foundgine.GraphQL.HotChocolate`.

```text
GraphQL document
      ↓
operationName selection
      ↓
selected OperationDefinitionNode
      ↓
existing adapter translation
      ↓
SemanticRequest / MutationIntent
```

No operation-name or GraphQL AST types are introduced into Foundgine's provider-neutral semantic, planning, execution, or SQL layers.
