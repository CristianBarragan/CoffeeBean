# M17 — Adversarial Agent Harness

M17 tests the boundary between probabilistic agent output and Foundgine's deterministic semantic execution surface.

## Trust model

```text
Human / Agent intent
        |
        | untrusted
        v
Structured intent adapter
        |
        +-- syntax validation
        +-- depth/fan-out limits
        +-- filter limits
        +-- JSON value limits
        +-- unknown-property rejection
        v
Semantic resolution
        |
        +-- capability validation
        +-- authorization
        +-- plan construction
        +-- plan invariants
        v
Provider execution
```

The agent never supplies tenant identity, authorization predicates, provider selection, SQL, connection information, or other execution authority.

## Attack classes covered

- execution-control injection (`tenantId`, `userId`, provider, SQL, authorization fields)
- excessive relationship depth
- selection fan-out exhaustion
- filter-depth exhaustion
- filter-node exhaustion
- hidden-field discovery
- read/write capability confusion
- mutation-effect confusion
- deterministic structured-intent handling

## Unknown properties

The canonical JSON adapter now rejects unknown properties by default. This is intentional: a model-controlled property that is not part of the canonical intent contract should fail closed instead of being silently interpreted as future authority.

A permissive mode remains available for compatibility migrations, but it should not be used at an agent trust boundary.

## What M17 proves

M17 demonstrates that hostile structured output is bounded and cannot directly introduce execution authority before semantic resolution.

It does **not** prove that an LLM cannot misunderstand natural language. The model remains outside Foundgine's deterministic trust boundary.

It also does not replace provider-level authorization, database permissions, network security, authentication, secrets management, or deployment controls.

## Next gate

**M17.1 — Black-Box Adversarial Engine Testing** runs the hostile corpus through the complete Foundgine engine, provider-plan cache, SQL compiler, and database execution boundary. It also adds a PostgreSQL integration gate when `FOUNDGINE_POSTGRES_CONNECTION_STRING` is configured.

M17.2 should then add real model-provider replay fixtures so prompt-injection-shaped conversations are converted into hostile structured intents and tested end-to-end.

## Security principle

> Foundgine makes execution deterministic and policy-aware; it does not make interpretation infallible.
