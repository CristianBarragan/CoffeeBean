# Foundgine.Abstractions

Small contracts shared across Foundgine layers.

Contains stable IDs such as `EntityId`, `FieldId`, `RelationshipId`, and `ColumnId`, plus cross-layer mutation contracts.

No SQL, GraphQL, provider, or planner implementation belongs here.

### AOT authorization predicates

Authorization expressions are reduced at build time to `AuthorizationPredicate`.
The runtime never stores or compiles an expression tree. The predicate is a
small provider-independent tree that can be carried by a semantic connection
and lowered by a provider.
