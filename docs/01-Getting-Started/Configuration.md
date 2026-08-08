[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **Configuration**

# Configuration

## Contents

- [Connection strings](#connection-strings)
- [Kestrel and hosting](#kestrel-and-hosting)
- [Dependency injection wiring](#dependency-injection-wiring)
- [Startup warmup](#startup-warmup)

---

## Connection strings

Coffee Beanery reads standard ASP.NET Core configuration. The sample's
`appsettings.json` looks like:

```json
{
  "ConnectionStrings": {
    "BankingConnectionString": "Host=localhost:5432;Database=BankingDB;Username=sa;Password=123456"
  },
  "Kestrel": {
    "EndPoints": { "Http": { "Url": "http://localhost:4300" } }
  }
}
```

Replace credentials and host for your environment. Multiple bounded contexts can register
their own connection strings and their own `Database.Entity.*` / `Database.Graph.*` projects,
following the same pattern as `Database.Entity.Banking` / `Database.Graph.Banking`.

## Kestrel and hosting

The sample hosts Hot Chocolate over standard Kestrel/ASP.NET Core. Nothing about Coffee
Beanery's runtime requires a specific host model — see
[Architecture → Layers](../02-Architecture/Layers.md) for how GraphQL is treated as a
transport adapter rather than a hosting requirement.

## Dependency injection wiring

Registration follows a composition-root pattern: Foundation contracts, generated
registrations, runtime services, and the SQL/PostgreSQL provider are each registered in
their own extension method, called from `Program.cs`. See
[Dependency Injection → Registration](../07-Dependency-Injection/Registration.md) for the
full breakdown and lifetime guidance.

## Startup warmup

Before the first request is served, the runtime executes a warmup pass (`GraphWarmup.Init`)
that discovers all `IMappingSet` implementations, pre-resolves reflection-derived property
info, compiles getter/setter delegates, and pre-builds the node traversal tree — so no
reflection work is left on the request path. See
[Performance → Benchmarks](../10-Performance/Benchmarks.md#why-response-times-are-this-low)
for the mechanics and why it matters.

---

## Related Documentation

- [Dependency Injection](../07-Dependency-Injection/README.md)
- [Persistence](../08-Persistence/README.md)
- [Performance](../10-Performance/README.md)

---

← Previous: [First Service](First-Service.md)  |  Next: [FAQ](FAQ.md) →
