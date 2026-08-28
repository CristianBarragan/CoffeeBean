# Foundgine.Aot.Generator

A small Roslyn incremental generator that turns attributed domain types into Foundgine metadata.

The generator is compile-time infrastructure only.

## Generated semantic model

The generator emits `Foundgine.Generated.GeneratedSemanticModel.Model` as the authoritative AOT semantic model for discovered `FoundgineEntity` declarations. It is generated from the same compile-time entity graph used for storage metadata, preserving entity identity, fields, CLR types, relationships, and cardinality without runtime reflection.

Consumer-specific projections must consume this semantic model rather than becoming independent semantic authorities.
