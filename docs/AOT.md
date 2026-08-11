# AOT metadata

Foundgine can generate metadata at compile time with a Roslyn incremental generator.

```text
Domain types
   ↓
Foundgine.Aot attributes
   ↓
AOT generator
   ↓
Generated metadata provider
   ↓
Normal runtime pipeline
```

The generator emits metadata. It does not emit SQL, GraphQL, execution plans, or provider code.

The AOT path is covered by `tests/Foundgine.Aot.Tests` and the end-to-end tests.
