[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **First Service**

# Your First Service

## Contents

- [What you're running](#what-youre-running)
- [Anatomy of the sample](#anatomy-of-the-sample)
- [Write a mapping class](#write-a-mapping-class)
- [Run a query](#run-a-query)
- [What just happened](#what-just-happened)

---

## What you're running

The bundled sample, `example/HotChocolateCoffeeBeanery`, models a small Banking domain:
customers, accounts, contracts, and transactions. It's wired end to end through Coffee
Beanery's Phase 1 stack:

```
EF Core mapping classes  →  generated execution plan  →  Hot Chocolate (GraphQL)  →  Dapper  →  PostgreSQL
```

## Anatomy of the sample

| Project | Role |
|---|---|
| `Api/Api.Banking` | The ASP.NET Core host — Hot Chocolate endpoint, query/mutation resolvers |
| `Domain/CoffeeBeanery` | The framework runtime, wired into this specific solution |
| `Domain/CoffeeBeanery.GraphQL.Core.Foundation` | Foundation contracts (see [Foundation](../03-Foundation/README.md)) |
| `Domain/CoffeeBeanery.GraphQL.Core.Mapping.Generators` | The Roslyn source generator (see [Source Generators](../06-Source-Generators/README.md)) |
| `Domain/Domain.Model`, `Domain/Domain.Shared` | The business/domain model and shared mapping DSL |
| `Infrastructure/Database/*` | EF Core entity models, migrations, and the PostgreSQL/AGE providers |

## Write a mapping class

Business models are mapped to storage entities in a `partial` mapping class that derives
from `BaseModelMappingRegistration<T>`. `BuildMap()` is read by the generator at compile
time — it's the source of truth the generator parses, not code that runs at request time:

```csharp
public partial class ProductMapping : BaseModelMappingRegistration<Product>
{
    public ProductMapping() : base(alias: "product", modelName: nameof(Product)) { }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap { /* ... */ };
        map.AddModelToEntity<Product, ProductEntity>();
        map.FieldMaps.Add(new FieldMap { /* ... */ });
        return map;
    }
}
```

The generator emits the other half of the `partial class` — a compiled `Register()` override
that builds the node tree directly, with no reflection at runtime. See
[Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md) for the
full mechanics and the required base-class shape.

## Run a query

With the sample running (see [Installation](Installation.md)), open the GraphQL IDE and run:

```graphql
query {
  customers(first: 5) {
    nodes {
      id
      name
      accounts {
        nodes { id balance }
      }
    }
  }
}
```

## What just happened

1. Hot Chocolate parsed the GraphQL request and handed it to Coffee Beanery's [runtime](../04-Runtime/README.md).
2. The generated [query planner](../04-Runtime/Queries.md) resolved the requested fields against
   compile-time metadata — no reflection, no runtime type discovery.
3. A single batched SQL statement was built and executed via Dapper against PostgreSQL.
4. Rows were mapped back to domain models using pre-compiled delegates (see
   [Performance → Benchmarks](../10-Performance/Benchmarks.md) for why this step has no
   reflection cost).
5. Hot Chocolate serialized the result graph back to the client.

Continue to [Configuration](Configuration.md) to see how connection strings, DI, and warmup
are wired together.

---

## Related Documentation

- [Configuration](Configuration.md)
- [Runtime](../04-Runtime/README.md)
- [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md)
- [Samples](../11-Samples/README.md)

---

← Previous: [Installation](Installation.md)  |  Next: [Configuration](Configuration.md) →
