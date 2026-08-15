# Foundgine.Semantics

Provider-independent application meaning.

Owns:

- semantic entities and relationships;
- semantic requests and graphs;
- request resolution;
- authorization;
- query controls.

It does not know GraphQL, SQL, or a database.


## Mutation Semantic IR

Mutation semantics are represented under `Mutation/` using semantic entity, field and relationship identities. Physical columns and provider mutation plans remain outside the semantic layer.
