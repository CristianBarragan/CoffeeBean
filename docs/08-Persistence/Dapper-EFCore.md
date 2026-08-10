# Dapper and EF Core

Foundgine is not an EF Core/Dapper hybrid.

The current provider proof uses `Microsoft.Data.Sqlite` directly.

EF Core and Dapper may be useful integrations in an application, but neither is required by the current core execution proof.

Possible future arrangements include:

```text
EF Core
   ↓
application metadata / persistence

Foundgine
   ↓
execution plan

Dapper / ADO.NET / provider API
   ↓
database
```

The key boundary remains the provider contract.
