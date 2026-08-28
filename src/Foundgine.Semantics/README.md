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
## Manual semantic authoring

Manual semantic models can be strongly typed against the application/domain model. For example:

```csharp
new SemanticModelBuilder()
    .Entity<Product>(productId, "Product", e => e
        .Identity(x => x.Id)
        .Field(x => x.Sku)
        .Field(x => x.Name));
```

The selector parameter `x` is the `Product` model type. It is not the semantic entity builder, an EF entity metadata type, or provider metadata. Foundgine derives the semantic field name and CLR type from the selected model property and assigns the entity-local field identity. The older `Field(new FieldId(...), name, typeof(...))` overload remains available for low-level/manual construction and compatibility.

Relationships can use the same strongly typed approach on both sides. The generic arguments are explicitly `<fromEntity, toModel>`, while each selector is compiled against its corresponding domain model:

```csharp
new SemanticModelBuilder()
    .Relationship<Product, ProductComponent>(
        Product, new RelationshipId(1), "components",
        product => product.Id,
        Component, component => component.ParentProductId,
        RelationshipCardinality.Many);
```

Foundgine validates that both selectors are direct properties and that their CLR types match. `product => product.Id` is rooted in `Product`; `component => component.ParentProductId` is rooted in `ProductComponent`. This is deliberately separate from EF entity metadata or semantic-builder properties.

