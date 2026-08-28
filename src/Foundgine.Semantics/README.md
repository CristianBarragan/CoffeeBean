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


## Authorization configuration

Authorization primitives belong to Foundgine, while applications provide only policy configuration and actor context. `SemanticAuthorizationConfiguration` and `ConfiguredSemanticAuthorizationPolicy` are provider-neutral and can be reused by query, dynamic intent, MCP, GraphQL, and mutation paths.

```csharp
var configuration = new SemanticAuthorizationConfiguration()
    .AddEntityRule((ctx, entity, operation) => /* application rule */ true)
    .AddFieldRule((ctx, entity, field, operation) => /* application rule */ true)
    .AddRelationshipRule((ctx, entity, relationship, operation) => /* application rule */ true);

var policy = new ConfiguredSemanticAuthorizationPolicy(
    configuration,
    new SemanticAuthorizationContext(tenantId, role, claims));
```

Rules are evaluated after open intent is resolved. Logical traversals are expanded into their complete semantic relationship path before authorization, so a convenient path such as `Customer -> transactions` cannot bypass policy on intermediate entities or relationships.

Attributes such as `SemanticPolicyAttribute`, `SemanticEntityAttribute`, and `SemanticFieldAttribute` are optional Foundgine primitives. Configuration is preferred when security or semantic meaning is application policy rather than domain-model metadata.

## Structural discovery and application configuration

Foundgine keeps four concerns separate:

- **Metadata** describes what exists: entities, fields, identities and direct relationships.
- **Semantic configuration** describes application meaning that structural metadata cannot infer, such as a logical traversal.
- **Authorization** describes what an actor may exercise.
- **Intent** describes what the caller wants.

When structural metadata is available, applications can start with `SemanticModelBuilder.FromMetadata(...)` and add only semantic meaning. Logical traversals can be configured by semantic names, avoiding dependencies on generated numeric identities:

```csharp
var model = SemanticModelBuilder
    .FromMetadata(metadata)
    .Traversal("Customer", "transactions", "relationships", "contract", "transactions")
    .Build();
```

The named traversal is expanded into its underlying relationship path before authorization and planning. No shortcut capability is granted merely because the traversal has a convenient caller-facing name.
