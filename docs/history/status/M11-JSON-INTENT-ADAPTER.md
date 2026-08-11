# M11 — JSON Structured Intent Adapter

## Goal

Prove that Foundgine can accept a second independent producer format without duplicating semantic resolution, authorization, planning, or provider logic.

## Boundary

```text
JSON
  ↓
JsonReadIntentAdapter
  ↓
ReadIntent
  ↓
ReadIntentCompiler
  ↓
SemanticRequest
  ↓
Resolution
  ↓
Authorization
  ↓
Planning
  ↓
Provider
```

The JSON adapter owns only wire-format concerns:

- property names
- filter discriminators (`field`, `relationship`, `and`, `or`)
- JSON value normalization
- basic structural validation

It does not know:

- SQL
- database tables or columns
- GraphQL
- authorization policy
- execution plans

## JSON shape

The intentionally small wire format uses:

- `rootEntity`
- `selections`
- `filter`
- `order`
- `limit`
- `offset`
- `after`

Selections contain either `field` or `relationship`, with `children` for relationship selections.

Filters are discriminated by `kind`:

- `field`
- `relationship`
- `and`
- `or`

Values are normalized to ordinary CLR values (`string`, `bool`, integer/decimal numbers, arrays, dictionaries, or `null`) before semantic compilation.

## Acceptance criterion

The E2E test sends JSON through the adapter and then executes the exact same M1–M5 pipeline used by other producers against SQLite. No JSON-specific logic appears after `ReadIntentCompiler`.

## Why this matters

M10 identified the strongest Foundgine value proposition as a deterministic semantic execution substrate for multiple intent producers. M11 proves the producer boundary with a concrete second format rather than an additional framework abstraction.
