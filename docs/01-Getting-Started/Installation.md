[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **Installation**

# Installation

## Contents

- [Prerequisites](#prerequisites)
- [Clone the repository](#clone-the-repository)
- [Set up PostgreSQL and Apache AGE](#set-up-postgresql-and-apache-age)
- [Restore and build](#restore-and-build)
- [Verify](#verify)

---

## Prerequisites

Coffee Beanery targets **.NET 9** (`net9.0`) and is built and tested on x64. You'll need:

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- **PostgreSQL** with the **Apache AGE** extension enabled (used for the graph read path — see [Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md))
- Optionally, a local Redis or FasterKV-compatible cache — the sample uses `FasterKv.Cache.Core` in-process, so no external cache server is required to get started

Coffee Beanery's runtime package pulls in Hot Chocolate, Dapper.Contrib, EF Core (design-time,
for mapping metadata), Npgsql, AutoMapper, and Z.Dapper.Plus. You don't need to install these
separately — `dotnet restore` handles it.

## Clone the repository

```bash
git clone https://github.com/coffee-beanery/coffee-beanery.git
cd coffee-beanery
```

The repository has two top-level trees:

- `src/CoffeeBeanery` — the framework itself
- `example/HotChocolateCoffeeBeanery` — a full sample application (Banking domain) that
  exercises the framework end to end

## Set up PostgreSQL and Apache AGE

1. Provision a PostgreSQL instance (local Docker container or a managed instance).
2. Install and enable the [Apache AGE](https://age.apache.org/) extension on the target database.
3. Create the database referenced by the sample's connection string (`BankingDB` by default —
   see `example/HotChocolateCoffeeBeanery/Api/Api.Banking/appsettings.json`).
4. Apply the EF Core migrations under `Infrastructure/Database/Database.Entity.Banking/Migrations`
   and `Infrastructure/Database/Database.Graph.Banking/Migrations`.

Update `ConnectionStrings:BankingConnectionString` in `appsettings.json` to point at your instance.

## Restore and build

```bash
cd example/HotChocolateCoffeeBeanery
dotnet restore
dotnet build
```

The build triggers the [mapping source generator](../06-Source-Generators/Mapping-Generator.md),
which reads your EF Core mapping classes and emits the compile-time execution plan. If the
generator reports a diagnostic (`CBMAP00x`), see
[Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md) before continuing.

## Verify

```bash
dotnet run --project Api/Api.Banking
```

Open `http://localhost:4300/graphql` (the port is set in `appsettings.json` under `Kestrel`)
to reach the Banana Cake Pop GraphQL IDE. If the schema loads and you can run an introspection
query, installation succeeded — continue to [First Service](First-Service.md).

---

## Related Documentation

- [First Service](First-Service.md)
- [Configuration](Configuration.md)
- [Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md)

---

← Previous: [Getting Started](README.md)  |  Next: [First Service](First-Service.md) →
