# M7 — GraphQL Adapter Freeze

M7 proves that GraphQL is an adapter over Foundgine, not a second engine.

## Frozen path

```text
Hot Chocolate AST
       ↓
HotChocolateSemanticAdapter
       ↓
SemanticRequest
       ↓
Resolution → Authorization → Planning → Execution → Provider
```

The query adapter references only the semantic/model contracts needed to translate a GraphQL query. It does not reference Planning, Execution, SQL, or mutation planning.

## Mutation isolation

The existing mutation adapter remains available, but is isolated in `Foundgine.GraphQL.HotChocolate.Mutations`. Mutation planning is post-M7 functionality and therefore cannot force the M7 query adapter to depend on `Foundgine.Planning`.

## M7 scope

Supported by the frozen query adapter:

- one query operation
- one root field
- scalar selections
- relationship selections
- transparent inline fragments
- semantic query arguments already represented by `SemanticQueryOptions`
- GraphQL meta-field omission

Deferred until their semantic contracts are independently justified:

- named fragments
- aliases
- directives
- variables
- subscriptions
- Relay wrappers

The adapter must not grow a second execution model merely to mirror GraphQL features.
