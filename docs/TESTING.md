# Testing

Tests are the executable definition of the current architecture.

## Run everything

```bash
dotnet test
```

## Test layers

- `Foundgine.Semantics.Tests` — semantic model and authorization.
- `Foundgine.Planning.Tests` — planning boundaries.
- `Foundgine.Aot.Tests` — generated metadata.
- `Foundgine.Intent.Json.Tests` — JSON input boundary.
- `Foundgine.GraphQL.HotChocolate.Tests` — GraphQL adapter behaviour.
- `Foundgine.E2E.Tests` — complete runtime paths.

## What to test

Prefer tests at the narrowest useful boundary, then add an end-to-end test when a capability crosses layers.

Do not weaken a test just to make an implementation pass. Change the implementation when the contract is correct; change the test when the contract itself was wrong.
