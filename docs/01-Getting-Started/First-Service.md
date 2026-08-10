# First Service

The current repository is intentionally proof-oriented rather than packaged as a complete ASP.NET Core hosting framework.

The smallest useful composition is:

```text
Metadata
   ↓
Semantic model
   ↓
Resolver
   ↓
Read intent
   ↓
Planner
   ↓
Provider
```

The Banking sample demonstrates this composition directly.

## Recommended application shape

Keep application code responsible for:

- constructing/registering domain metadata;
- defining semantic overrides;
- choosing candidate sources;
- composing providers;
- integrating with the application's existing DI/hosting system.

Do not introduce a Foundgine-specific hosting abstraction until a real use case requires one.

## First proof

Start with:

```text
Customer
 → Account
 → Transaction
```

and make one read work against a real database before adding transports.
