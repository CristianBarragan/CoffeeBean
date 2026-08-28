# Foundgine.Aot.Generator

A small Roslyn incremental generator that turns attributed domain types into Foundgine metadata.

The generator is compile-time infrastructure only.
### Structural metadata contract

The AOT producer is a compile-time structural contract, not a passive serializer. Relationship declarations are rejected when the target entity, navigation target, foreign-key property, principal-key property, or key types are inconsistent. This keeps invalid topology out of `GeneratedMetadata.Registry` before semantic discovery or authorization can consume it.

