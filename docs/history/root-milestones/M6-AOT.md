# M6 — AOT

## Boundary

```text
Domain types
    ↓
Foundgine.Aot attributes
    ↓
Roslyn source generator
    ↓
Generated Foundgine.Metadata
    ↓
IMetadataProvider
    ↓
M1–M5 runtime pipeline
```

## Ported techniques

The archive was used only as an implementation reference for:

- entity discovery
- scalar/property discovery
- deterministic identity emission
- column mapping
- relationship discovery
- foreign-key/principal-key mapping
- generated metadata provider seam

The old generator architecture was not copied.

## Identity rule

Explicit IDs can be supplied when a domain wants stable published identities. When omitted, the generator derives deterministic 16-bit IDs from the fully qualified symbol/property name using FNV-1a followed by collision resolution.

## Relationship rule

A relationship declaration supplies the target type, foreign-key property and principal-key property. The generator resolves the foreign key on the target entity when it is not present on the source entity. This supports declarations such as `Customer.Accounts -> Account.CustomerId = Customer.Id` without introducing join concepts into the domain type itself.

## Runtime rule

The generated provider implements `IMetadataProvider` and `IMetadataSource`. Runtime code consumes the interface; Roslyn is not part of the runtime execution path.

## Acceptance

`Foundgine.Aot.Tests` contains a compile-time generator proof. The test domain contains Customer and Account, and the generated metadata is inspected for entity names, storage names, fields, columns, relationship endpoints, and join key mappings.

`dotnet test` should be run locally to validate the generator because the development environment used to prepare this archive does not contain the .NET CLI.
