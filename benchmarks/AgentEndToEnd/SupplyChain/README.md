# Supply Chain E2E — AI Agent → MCP → Foundgine → PostgreSQL

A reproducible end-to-end supply-chain benchmark exercising the Foundgine layers from an agent-like bot through MCP, semantic modeling, authorization, planning, execution and PostgreSQL.

## Flow

AI agent bot → MCP → Foundgine capability boundary → semantic model → authorization → semantic planner → execution service → Npgsql → PostgreSQL.

The bot supports up to five customer identities plus Bob, Carol, Dave and Admin, and deliberately mixes valid, invalid and unauthorized operations. The benchmark verifies that denied requests do not mutate PostgreSQL and that successful mutations produce the expected state. The execution path now explicitly lowers semantic plans to Foundgine ExecutionIR before the PostgreSQL boundary, with a plan fingerprint included in receipts.

## Actors

- Alice — Customer; own orders/customer-visible product and shipment reads; create/cancel own orders.
- Bob — Customer Service; customer/order/shipment reads and order cancellation.
- Carol — Warehouse Operator; inventory and shipment operations.
- Dave — Procurement; suppliers/products/inventory operations.
- Admin — unrestricted.

## First vertical slice

`PlaceOrder` is the high-assurance mutation:

1. actor authorization
2. customer ownership check
3. product resolution
4. positive quantity validation
5. inventory availability check
6. server-side price calculation
7. atomic order + order_items + inventory decrement
8. idempotency/replay protection
9. receipt/evidence

Queries additionally exercise relationship traversal such as Customer → Orders → OrderItems → Product and Product → Supplier/Category/Inventory.

## Current coverage

MCP capabilities include capability discovery, customer/order/product/shipment reads, inventory reads and writes, supplier/product/customer listing, order creation/cancellation, shipment creation/status updates, and the high-assurance `PlaceOrder` transaction. Order fulfillment records its warehouse allocation so cancellation restores inventory to the correct warehouse.

## Run

```powershell
cd benchmarks/AgentEndToEnd/SupplyChain
$env:SUPPLY_CHAIN_CUSTOMERS="5"
$env:SUPPLY_CHAIN_STEPS="25"
$env:SUPPLY_CHAIN_SEED="20260823"
./run-supply-chain.ps1
```

The runner starts PostgreSQL and the Foundgine MCP service, seeds the graph, executes a stochastic agent workload, and writes `reports/supply-chain-report.json` and `reports/supply-chain-report.md`.

## Publish the report to the website

After a successful run, publish the generated report into the website asset folder:

```powershell
./publish-supply-chain-report.ps1
```

This copies the JSON and Markdown report to `docs-site/assets/agent-benchmark/supply-chain/` and writes a publication manifest. The website page at `docs-site/agent-benchmark/supply-chain/index.html` reads the JSON directly and renders the latest published run.


## How this fits into the Foundgine story

The Supply Chain benchmark is deliberately the **end of the story**, not the beginning. It takes the lower-level guarantees already tested by the repository and places them in a realistic agent-facing business workflow:

```text
Unit semantics / planning / authorization tests
                    ↓
       PostgreSQL integration tests
                    ↓
     Authorization penetration tests
                    ↓
 Adversarial semantic-input penetration tests
                    ↓
       Performance smoke / benchmark
                    ↓
     Supply Chain agent-facing E2E
                    ↓
       Agent → MCP → Foundgine → PostgreSQL
```

The benchmark therefore answers a different question from a raw throughput test: **can an application expose useful business capabilities to an agent without handing the agent authority over the application's data-access and execution rules?**

### What is inside the boundary

The agent chooses a capability and supplies structured arguments. MCP exposes the application capability surface. Foundgine resolves the request against the semantic model, applies authorization and validation, builds the semantic plan, lowers it through ExecutionIR, and hands the executable boundary to the provider. PostgreSQL remains the system of record.

The benchmark deliberately exercises the failure paths as well as successful paths. Unauthorized or invalid requests are expected to fail, and the benchmark checks that rejected operations do not create unintended database state.

### Verification status

The repository CI treats the following as release-quality gates: **unit tests, PostgreSQL integration tests, authorization penetration tests, adversarial semantic-input tests, and a real performance smoke test**. The Supply Chain E2E is an additional stateful product benchmark. See [`VERIFY-GATES.md`](VERIFY-GATES.md) for the exact gate definitions and local commands.

Do not read the Supply Chain report as a replacement for those gates. It is the final application-level demonstration that brings them together.
