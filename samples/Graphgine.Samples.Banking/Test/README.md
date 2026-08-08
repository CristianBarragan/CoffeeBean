# Integration tests (Apidog)

Black-box integration tests for `Api.Banking`, run through [Apidog](https://apidog.com)
against a live instance of the sample. They POST GraphQL `wrapper` mutations/queries at
`http://localhost:4300/graphql/` — the exact address `Api.Banking`'s `http` launch
profile binds to (see `../Api/Api.Banking/Properties/launchSettings.json`) — and assert
on the JSON response, so they're a real end-to-end check of the whole stack: `Graphgine`
→ `Graphgine.HotChocolate` → EF Core/Npgsql → Postgres/AGE.

## Files

- **`Graphgine.Banking.apidog.json`** — the full Apidog project: all 5 scenarios,
  the request collection, and the `Develop`/`Testing`/`Prod` environments. This is the
  source of truth — open it in Apidog to edit a scenario or add a new one.
- **`<Scenario>.apidog-cli.json`** — one file per scenario, exported from the project
  above via Apidog's *Export as CLI runner* feature. These are what actually run in CI;
  regenerate one after editing its scenario in the project file (Apidog → the scenario →
  Export → CLI runner). Don't hand-edit them.
- **`Test_Results.png`** — a reference screenshot of a passing run.

## Scenarios

| Scenario | Exercises |
|---|---|
| `Customer` | `wrapper` mutation/query round-trip for a single `Customer`, `Product`, and `Contract`, keyed by `where: { customerKey: { eq } }` |
| `Multiple Customers` | Same shape, filtered with `where: { customerKey: { in: [...] } }` across 3 customers |
| `Customer Graph` | `CUSTOMER_CUSTOMER_EDGE`/`INNER_CUSTOMER` — the Apache AGE graph traversal path (`CustomerCustomerRelationship`) rather than the relational path |
| `Contact Point` | `ContactPoint` (email) attached to a `Customer` |
| `Product` | Same as `Contact Point` — kept as a separate scenario in the source project; consider merging if it's still a duplicate the next time this suite is touched |

Each scenario's dataset uses Apidog's built-in fakers (`{{$string.uuid}}`,
`{{$person.firstName}}`, `{{$internet.email}}`, ...) so a run generates fresh data
rather than depending on fixture rows already existing in the database.

## Running

1. Start Postgres/AGE and run EF Core migrations for `Database.Entity.Banking` (see the
   sample's top-level README/`../README.md` if present, or `Database.Entity.Banking`'s
   `Migrations/` folder).
2. `dotnet run --project ../Api/Api.Banking --launch-profile http` — leaves the API
   listening on `http://localhost:4300/graphql/`.
3. `apidog run "Customer.apidog-cli.json"` (repeat per scenario), or import
   `Graphgine.Banking.apidog.json` into the Apidog app and run the whole collection
   from there.

## Provenance

Ported from a standalone Apidog project (`Coffee Beanery`, pre-dating the
Foundgine/Graphgine rename) that had only ever been partially exported — `Customer` and
`Multiple Customers` were already checked in here as `.apidog-cli.json`;
`Customer Graph`, `Contact Point`, and `Product` didn't have exports yet. Both the
project file and the newly-generated exports have been renamed and de-branded
(`info.name` inside the project changed from `Coffee Beanery` to `Graphgine Banking
Sample`); no GraphQL query, field name, or assertion was changed — the `wrapper`
mutation's `model:` enum values (`CUSTOMER`, `CONTACT_POINT`, ...) were already the
`Domain.Model.Model` enum's names, not old product branding, so nothing there needed to
change.
