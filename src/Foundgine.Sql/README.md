# Foundgine.Sql

The current SQL provider.

It translates Foundgine plans into parameterized SQL and executes them through ADO.NET.

SQL-specific concepts start here; they do not leak into the semantic or planning layers.
## Install

```bash
dotnet add package Foundgine.Sql
```

## Package scope

The SQL provider is currently proven end-to-end with SQLite. The repository also contains PostgreSQL-specific compilation and benchmark paths, including batched mutation compilation. This is not a claim of universal relational-database support.

## Repository documentation

- [Current status](https://github.com/CristianBarragan/Foundgine/docs/CURRENT-STATUS.md)
- [Security](https://github.com/CristianBarragan/Foundgine/docs/SECURITY.md)
- [NuGet packaging](https://github.com/CristianBarragan/Foundgine/docs/NUGET-PACKAGING.md)

