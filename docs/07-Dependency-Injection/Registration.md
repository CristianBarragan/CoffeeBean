# Registration

There is no required `AddFoundgine()` hosting package in the current proof.

An application can compose the core services directly.

A future integration might provide:

```csharp
services.AddFoundgine(...);
```

but that should remain an optional adapter.

The core contracts must remain usable without ASP.NET Core or a specific DI container.
