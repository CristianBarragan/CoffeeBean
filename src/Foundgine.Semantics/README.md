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

## Semantic pipeline

The semantic layer now treats resolution as a correctness boundary rather than
just name lookup:

`Resolve → Validate → Normalize → Canonical Semantic IR → Authorization → Planning`

`SemanticType` and `SemanticFieldCapabilities` describe provider-independent
meaning, and semantic value validation rejects incompatible scalar/list values
before planning. `SemanticQueryOptionsValidator` rejects invalid pagination controls,
and cursor resolution adds the root identity as a deterministic tie-breaker.
`SemanticGraphValidator` proves relationship/target consistency before the graph
is compiled. The existing `ClrType` remains available as a compatibility bridge
for provider adapters; new semantic code should prefer `EffectiveSemanticType`.

Authorization remains deliberately separate from request resolution. The engine
validates the request's security context against the resolved semantic operation,
then applies authorization before secured planning. This keeps security as an
authoritative semantic invariant without making the resolver responsible for
transport-specific warrant verification.
