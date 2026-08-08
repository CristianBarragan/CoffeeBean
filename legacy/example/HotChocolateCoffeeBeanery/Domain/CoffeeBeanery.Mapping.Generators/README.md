# CoffeeBeanery.GraphQL.Core.Mapping.Generators

Source generator that replaces `NodeBuilder<TContext>`'s five reflective passes
(`InferModelChildren`, `GenerateReflectedFieldMaps`, `ResolveFieldMapAliases`,
`BuildTree`, `BuildModel`) with a compile-time equivalent, so the mapping layer
is Native AOT / trim safe with zero runtime reflection.

**Status: not yet build-verified.** This sandbox has no .NET SDK, so the project
has been written and self-reviewed carefully but not compiled against your real
`CoffeeBeanery.GraphQL.Core.Mapping` / `.Sql` assemblies. Treat the first build
as the real validation step — see "Known risk areas" below for where I'd look
first if something doesn't compile.

## Required changes to existing hand-written code

1. **Mapping classes must be `partial`.**
   ```csharp
   public partial class ProductMapping : BaseModelMappingRegistration<Product>
   ```
   The generator emits the other half of the partial class containing the
   generated `Register()` override.

2. **`BaseModelMappingRegistration<T>.Register()` must be `virtual`.**
   The generated partial provides `public override void Register()`, which
   builds `ModelNodeTree` / `CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree` / `ModelNode` / `EntityNode`
   directly and calls `NodeRegistry.RegisterNode(...)` — it never calls
   `BuildMap()` or touches `NodeBuilder` at runtime. `BuildMap()` itself stays
   in your hand-written file purely as the *source of truth the generator
   parses at compile time* — it's read, never executed.

3. **`BaseModelMappingRegistration<T>` must expose the constructor's alias and
   model strings** as `protected string Alias` / `protected string ModelName`
   (rename in `NodeTreeEmitter.cs` if your actual property names differ —
   search for `this.Alias` / `this.ModelName`).

4. **Reference the generator as an analyzer**, not a normal assembly reference:
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\CoffeeBeanery.GraphQL.Core.Mapping.Generators\CoffeeBeanery.GraphQL.Core.Mapping.Generators.csproj"
                        OutputItemType="Analyzer"
                        ReferenceOutputAssembly="false" />
   </ItemGroup>
   ```

5. **Drop the `NodeBuilder<TContext>.BuildFromMappings()` call from startup.**
   Registration now happens per-instance via each mapping class's generated
   `Register()` override, wherever `new ProductMapping(...).Register()` is
   already called today (e.g. from `ProductMappingSet.Register`). Nothing
   about that call site needs to change — only what `Register()` *does*
   changes.

## Ambiguous navigation handling

Where `NodeBuilder.BuildEntityChildren` threw `InvalidOperationException` at
runtime for ambiguous navigations (e.g. an entity with two navigation
properties to the same related type), this generator instead emits a build
**error** (`CBMAP003`) pointing at the entity. Resolve it the same way as
before (a `ModelToEntity` alias entry matching the navigation name), or via
the new `[EntityForeignKey]` escape hatch for navigations not expressible via
the `{Nav}Key` / `{Related}Key` convention at all (e.g. only configured via
fluent EF `OnModelCreating`):

```csharp
[EntityForeignKey(typeof(Customer), foreignKeyProperty: "InnerCustomerKey",
    principalKeyProperty: "CustomerKey", navigationName: "InnerCustomer")]
[EntityForeignKey(typeof(Customer), foreignKeyProperty: "OuterCustomerKey",
    principalKeyProperty: "CustomerKey", navigationName: "OuterCustomer")]
public class CustomerCustomerRelationship { ... }
```

`EntityForeignKeyAttribute` is emitted automatically via
`RegisterPostInitializationOutput` — you don't need to add it by hand or
reference any extra package.

## Diagnostics

| Id      | Mirrors (old runtime behavior)                                    | Severity |
|---------|---------------------------------------------------------------------|----------|
| CBMAP001 | `NodeBuilder` "WARNING: ... is type-incompatible with ..."        | Warning  |
| CBMAP002 | `NodeBuilder` "WARNING: ... has no matching property..."          | Warning  |
| CBMAP003 | `NodeBuilder.BuildEntityChildren` ambiguous-navigation exception   | **Error**|
| CBMAP004 | (new) navigation-shaped property with no resolvable FK by convention | **Error**|
| CBMAP005 | (new) unsupported `BuildMap()` statement shape                    | **Error**|

## Known risk areas to check on first build

- **`MappingClassParser`**: only understands the exact statement shapes used
  in `ProductMapping.BuildMap()` (local `NodeMap` declaration with object
  initializer, `AddModelToEntity<,>(...)`, `FieldMaps.Add(new FieldMap{...})`,
  `ExcludedFieldMappings.Add(...)`, `UpsertKeys.Add(...)`, `return map;`). Any
  other mapping class with a different `BuildMap()` shape (loops, conditionals,
  helper method calls) will hit `CBMAP005` and needs the parser extended.
- **Enum dictionary parsing** (`EvaluateEnumDictionary`/`TryEvaluateEnumToString`/
  `TryEvaluateEnumCast`) is the most speculative part of the parser — it's
  pattern-matching `{ Enum.Value.ToString(), (int)Enum.Value }` collection
  initializer entries syntactically. If your real `FromEnum`/`ToEnum`
  dictionaries are built differently than `ProductMapping`'s example, this
  needs adjusting.
- **`EntityNavigationConvention`**'s principal-key convention
  (`"{RelatedType.Name}Key"`) is an assumption based on the sample mapping
  (`ContractKey`, `AccountKey`, etc.) — verify it holds across your full
  entity set, since this is the one pass with no equivalent in the original
  `NodeBuilder` (which got this from live EF metadata, not convention).
- **`required` members on netstandard2.0** — `Polyfills.cs` defines the
  `RequiredMemberAttribute`/`CompilerFeatureRequiredAttribute`/`IsExternalInit`
  shims needed for C# 11 `required`/`init` on a netstandard2.0 TFM. If your
  build pulls in another package defining these same internal types
  (unlikely, but possible with multiple generator projects in one solution),
  you'll get a duplicate-type error — delete one copy.
- **`BaseModelMappingRegistration<T>` field names** — I assumed `Alias` and
  `ModelName` based on the constructor signature shown
  (`ProductMapping(string alias, string model)`). Confirm/rename in
  `NodeTreeEmitter.EmitRegisterOverride`.
