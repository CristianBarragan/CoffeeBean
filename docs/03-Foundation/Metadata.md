# Metadata

`Foundgine.Metadata` is the bridge between application concepts and physical storage.

## Core concepts

```text
EntityMetadata
ColumnMetadata
RelationshipMetadata
JoinMetadata
JoinGraph
ModelMetadata
```

An entity can have:

- a logical identity;
- fields/columns;
- a physical storage name;
- relationships;
- mutation metadata.

## Why metadata matters

The planner should not contain:

```csharp
if (entity == Customer)
    join Account;
```

Instead it asks the metadata/join graph how entities connect.

## Physical schema independence

The E2E suite proves that logical names and physical storage names can differ.

For example:

```text
Customer
Account
Transaction
```

can map to an unrelated physical schema through metadata.

That separation is central to the architecture.
