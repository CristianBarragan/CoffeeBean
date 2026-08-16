# M17.2 — Model-Provider Replay Harness

## Purpose

M17.2 closes the gap between parser-level adversarial tests and realistic model output. The replay corpus treats structured JSON as untrusted output from an AI model and drives that output through the real Foundgine engine.

The invariant is not that a model will produce correct intent. The invariant is that **incorrect or hostile intent cannot acquire authority merely because it came from a model**.

## Pipeline

```text
model output fixture
        ↓
untrusted JSON adapter
        ↓
semantic intent
        ↓
authorization
        ↓
semantic plan
        ↓
provider plan/cache
        ↓
SQL compiler
        ↓
SQLite / PostgreSQL
        ↓
result + evidence + receipt
```

## Replay corpus

The initial corpus covers:

- normal tenant-scoped output
- explicit cross-tenant filtering
- SQL-injection-shaped values
- execution-control injection
- identity substitution

Execution-control and identity fields are rejected at the JSON trust boundary. Accepted hostile values remain data and cannot become SQL syntax or authorization context.

## Provider replay

SQLite provides a deterministic black-box test environment. PostgreSQL is exercised when `FOUNDGINE_POSTGRES_CONNECTION_STRING` is configured.

The same model-output fixture is therefore evaluated against both a lightweight provider and the real PostgreSQL boundary without changing the semantic policy.

## Cache invariant

A model-produced intent must never smuggle runtime authority into a reusable provider plan. Runtime tenant context remains execution-time data. The replay suite verifies that the same semantic shape can execute for different tenants without compiling a tenant-specific authorization value into the cached provider plan.

## What M17.2 proves

- model output is treated as untrusted
- hostile execution-control fields fail closed
- SQL-shaped values remain parameters
- tenant authorization remains semantic rather than model-controlled
- the same model output cannot escape tenant isolation by changing runtime context
- the provider boundary is exercised by replay rather than only isolated unit tests

## What it does not prove

M17.2 does not prove that an LLM is accurate, aligned, or immune to prompt injection. It proves that model output is not itself an authority boundary.

It also does not prove database security outside the configured provider, deployment security, credential security, network security, or correctness of an application's authorization policy.
