# Foundgine.Aot

`Foundgine.Aot` is the declaration and runtime-contract layer for Foundgine's AOT metadata pipeline.

It provides the attributes and small generated-metadata helper types used by application code. The companion `Foundgine.Aot.Generator` performs compile-time discovery and source generation.

## AOT architecture

```text
Application/domain declarations
        ↓
Foundgine.Aot attributes
        ↓
Foundgine.Aot.Generator
        ↓
generated metadata
        ↓
Foundgine.Metadata / Semantics
        ↓
Planning / Execution
```

The goal is to move stable structural knowledge into compile time rather than requiring runtime reflection-driven discovery.

## Core attributes

The package includes declarations such as:

- `FoundgineEntityAttribute`;
- `FoundgineFieldAttribute`;
- `FoundgineRelationshipAttribute`;
- `FoundgineModelAttribute`;
- `FoundgineConnectionAttribute`;
- `FoundgineConnectionMapAttribute`;
- `FoundgineModelEntityMapAttribute`;
- `FoundgineConversionAttribute`;
- `FoundgineAliasAttribute`;
- `FoundgineAuthorizationAttribute`;
- `FoundgineSemanticDimensionAttribute`;
- `FoundgineEventAttribute`.

## Entity and field declarations

Example:

```csharp
[FoundgineEntity("Customer")]
public sealed class CustomerRecord
{
    [FoundgineField("id", IsPrimaryKey = true)]
    public long Id { get; init; }

    [FoundgineField("name")]
    public string Name { get; init; } = "";
}
```

The attributes describe metadata. They do not execute a query and do not create objects at runtime.

## Relationships

A relationship declaration can describe physical key correspondence:

```csharp
[FoundgineRelationship(
    typeof(CustomerRecord),
    foreignKey: "CustomerId",
    principalKey: "Id")]
public CustomerRecord Customer { get; init; }
```

The generated metadata captures the structural topology for downstream consumers.

## Models and connections

Foundgine distinguishes a semantic/application model from a storage entity.

A model can expose a connection. The recommended pattern keeps the model free of any storage-type reference and maps the connection to its target explicitly, in the schema/infrastructure layer, via `FoundgineConnectionMapAttribute`:

```csharp
[FoundgineModel("Customer")]
public sealed class Customer
{
    [FoundgineConnection]
    public Order[] Orders => throw new NotSupportedException();
}

[FoundgineConnectionMap(typeof(Customer), nameof(Customer.Orders), typeof(OrderRecord))]
public sealed class ConnectionMapping;
```

`FoundgineConnectionAttribute` also has a legacy `(Type target)` constructor that couples the model directly to the storage type (`[FoundgineConnection(typeof(Order))]`). It is retained for compatibility; new code should prefer the parameterless attribute plus an explicit map.

The property is a declaration of topology. Foundgine does not evaluate it or construct an `Order`.

A connection therefore means:

> **This semantic model can visit this target.**

It does not mean:

> **This property is a runtime object-navigation implementation.**

## Stable identities

AOT declarations can carry explicit identities (`Id`, `ColumnId`, etc.).

Where an identity can be derived deterministically from stable semantic information, the generator can allocate it from that stable input rather than source ordering.

This makes generated metadata safer across source reordering and parallel development.

## Generated semantic field helpers

Generated fields can expose small helpers such as:

```csharp
CustomerFields.Name.Eq("Alice");
CustomerFields.Name.Neq("Bob");
CustomerFields.Name.In("Alice", "Bob");
CustomerFields.Name.Asc();
CustomerFields.Name.Desc();
CustomerFields.Name.Set("Alice");
```

These helpers construct semantic expressions/operations; they do not execute them.

## Compile-time validation

The generator validates structural consistency during compilation.

Typical failures include:

- duplicate identities;
- invalid relationships;
- missing referenced members;
- inconsistent key types;
- invalid declarations.

Compile-time failure is preferable to discovering a malformed metadata graph during production request execution.

## Native AOT goal

The AOT path is designed to reduce runtime dependence on reflection for stable metadata discovery.

It does not mean the entire Foundgine application is automatically Native-AOT-compatible merely because these attributes are used. Provider and application dependencies still need to satisfy their own AOT constraints.

## What belongs here

This package should remain focused on:

- declaration attributes;
- generated metadata contracts;
- tiny generated helper types.

The source generator implementation belongs in `Foundgine.Aot.Generator`.

## What does not belong here

Do not put:

- SQL generation;
- database connections;
- GraphQL;
- MCP;
- LLM calls;
- runtime authorization policy evaluation.

## Related packages

- `Foundgine.Aot.Generator` — compiler implementation.
- `Foundgine.Metadata` — metadata registry/discovery.
- `Foundgine.Semantics` — semantic model.
- `Foundgine` — application runtime.

## Target framework

- .NET 9
- MIT licensed
