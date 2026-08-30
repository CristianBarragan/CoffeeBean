# Foundgine.Intent.Json

`Foundgine.Intent.Json` is the thin JSON adapter for Foundgine read intent.

It converts a structured JSON request into the same provider-independent `ReadIntent` used by typed/dynamic application callers.

## Boundary

```text
JSON
  ↓
JsonReadIntentAdapter
  ↓
ReadIntent
  ↓
Foundgine.Semantics
  ↓
Authorization
  ↓
Planning
  ↓
Execution
```

The adapter stops at semantic intent.

It does not:

- authorize the caller;
- generate SQL;
- execute a database operation;
- decide which entity/field names are permitted;
- establish identity or tenant context.

## Basic shape

A request can describe:

```json
{
  "rootEntity": "Customer",
  "selections": [
    { "field": "Id" },
    { "field": "Name" }
  ],
  "limit": 50,
  "order": [
    { "field": "Name", "direction": "Asc" }
  ]
}
```

The adapter creates a `ReadIntent` from the structured representation.

Exact names are still resolved against the semantic model later.

## Supported intent concepts

The DTOs represent:

- root entity;
- field selections;
- nested relationship selections;
- field filters;
- logical filter expressions;
- relationship filters;
- relationship quantifiers;
- ordering;
- relationship-path ordering;
- limit;
- offset;
- cursor;
- aggregate ordering.

## Security and validation

JSON is untrusted input.

`JsonReadIntentAdapterOptions` provides structural limits including:

- `MaxSelectionDepth`;
- `MaxSelections`;
- `MaxFilterDepth`;
- `MaxFilterNodes`;
- `MaxJsonValueDepth`;
- `RejectUnknownProperties`.

The adapter should be configured conservatively for public endpoints.

These limits protect the parsing/intent boundary. Semantic authorization remains a separate mandatory step.

## Unknown properties

`RejectUnknownProperties` defaults to `true`.

This is useful for avoiding silently accepted request fields that the caller may believe have an effect when they do not.

## Example

```csharp
var adapter = new JsonReadIntentAdapter();

var intent = adapter.Parse("""
{
  "rootEntity": "Customer",
  "selections": [
    { "field": "Id" },
    { "field": "Name" },
    {
      "relationship": "Orders",
      "children": [
        { "field": "Id" },
        { "field": "Total" }
      ]
    }
  ],
  "filter": {
    "kind": "field",
    "field": "Name",
    "operator": "Eq",
    "value": "Alice"
  },
  "order": [
    { "field": "Name", "direction": "Asc" }
  ],
  "limit": 50
}
""");
```

`Parse` throws `InvalidOperationException` for structurally invalid or over-limit JSON, and `JsonException`-derived failures are wrapped into the same exception type so callers have one failure mode to handle at this boundary.

Custom limits can be supplied for stricter public endpoints:

```csharp
var adapter = new JsonReadIntentAdapter(new JsonReadIntentAdapterOptions
{
    MaxSelectionDepth = 6,
    MaxSelections = 40,
    MaxFilterDepth = 6,
    MaxFilterNodes = 40,
    MaxJsonValueDepth = 4,
    RejectUnknownProperties = true
});
```

## Related packages

- `Foundgine.Semantics` — defines `ReadIntent` and the semantic query/filter/order types the adapter targets.
- `Foundgine.AI` — uses this adapter to turn model-issued JSON into `ReadIntent`.
- `Foundgine.MCP` — uses this adapter for MCP-transported read requests.
- `Foundgine` — runtime facade that resolves, authorizes, plans, and executes the resulting `ReadIntent`.

## Target framework

- .NET 9
- MIT licensed
