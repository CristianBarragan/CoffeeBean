# M27 — GraphQL Error Semantics

M27 establishes a stable GraphQL-facing error boundary without introducing GraphQL
error types into Foundgine core.

## Contract

The adapters retain their existing throwing `Adapt(...)` APIs for compatibility.
Hosts that want structured client errors can use:

- `HotChocolateSemanticAdapter.TryAdapt(...)`
- `HotChocolateMutationAdapter.TryAdapt(...)`

Both return `GraphQLAdapterResult<T>`.

An unsuccessful result contains `GraphQLAdapterError` with:

- `Code`
- `Message`
- optional `Path`
- `Category`

The default category is `BAD_REQUEST`.

## Error classification

Expected adapter/input failures are classified as:

- `BAD_USER_INPUT` — variable/input/coercion failures
- `GRAPHQL_VALIDATION_FAILED` — unsupported/unknown fields, arguments, fragments
- `GRAPHQL_ADAPTER_ERROR` — other expected adapter contract failures

The mapping is intentionally conservative and remains at the GraphQL adapter
boundary. It is not part of SemanticRequest, MutationIntent, Planning, Execution,
or SQL.

## Runtime failures

M27 does not catch database, planner, authorization, or execution exceptions.
Those belong to their respective runtime boundaries and should be mapped by the
host execution layer with their own error policy.

## Example

```csharp
var result = adapter.TryAdapt(graphql, variables);

if (!result.Succeeded)
{
    foreach (var error in result.Errors)
    {
        // Map error.Code, error.Message and error.Path to the host GraphQL error.
    }
}
```
