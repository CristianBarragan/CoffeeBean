[Home](../../README.md) → [Documentation](../README.md) → **GraphQL**

# GraphQL

## Contents

- [Schema](Schema.md) — how the schema is composed from generated node metadata
- [Resolvers](Resolvers.md) — how resolvers hand off to the runtime
- [Pagination, Filtering & Sorting](Pagination-Filtering-Sorting.md)

---

## Where GraphQL fits

GraphQL — specifically [Hot Chocolate](https://chillicream.com/docs/hotchocolate) — is
**Phase 1's transport**, not a permanent architectural commitment. See
[Architecture → Vision](../02-Architecture/Vision.md): the planner underneath doesn't know
or care that GraphQL is asking it for data. REST and gRPC are listed as future transports in
the same [roadmap](../13-Reference/Roadmap.md) for exactly this reason — they'd enter the
same execution engine through a different door.

That said, GraphQL is what's built and tested today, via:

```xml
<PackageReference Include="HotChocolate" Version="15.1.12" />
<PackageReference Include="HotChocolate.AspNetCore" Version="15.1.12" />
<PackageReference Include="HotChocolate.Data" Version="15.1.12" />
<PackageReference Include="HotChocolate.Types" Version="15.1.12" />
<PackageReference Include="HotChocolate.Types.Analyzers" Version="15.1.12" />
```

## How a request enters

1. Hot Chocolate parses and validates the incoming GraphQL document against the schema.
2. A resolver (see [Resolvers](Resolvers.md)) hands the requested field selection to Coffee
   Beanery's [runtime](../04-Runtime/README.md) rather than resolving it field-by-field itself.
3. The runtime's [query planner](../04-Runtime/Queries.md) turns that selection into a single
   execution plan against the compile-time metadata produced by the
   [mapping generator](../06-Source-Generators/Mapping-Generator.md).
4. One batched SQL statement executes via Dapper; results are mapped back and returned
   through Hot Chocolate's serialization.

## Related Documentation

- [Runtime](../04-Runtime/README.md)
- [Source Generators](../06-Source-Generators/README.md)
- [Architecture → Vision](../02-Architecture/Vision.md)

---

← Previous: [Runtime](../04-Runtime/README.md)  |  Next: [Source Generators](../06-Source-Generators/README.md) →
