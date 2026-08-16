# M17.1 — Black-Box Adversarial Engine Testing

M17.1 closes the gap between testing the JSON adapter and testing the provider in isolation.

The security boundary is now exercised as one black-box pipeline:

```text
untrusted structured intent
        |
        v
JSON boundary
        |
        v
ReadIntent
        |
        v
semantic resolution
        |
        v
authorization
        |
        v
semantic plan
        |
        v
provider-plan cache
        |
        v
Execution IR
        |
        v
SQL compiler
        |
        v
SQL execution
        |
        v
database result + evidence + receipt
```

## What M17.1 adds

### 1. Hostile execution-control injection

The black-box corpus attempts to supply:

- `tenantId`
- `userId`
- `provider`
- `sql`
- `authorization`

These properties are rejected by the strict JSON adapter before semantic execution.

The agent therefore cannot turn structured intent into execution authority.

### 2. SQL-injection-shaped values

Agent-controlled values such as:

```text
' OR 1=1 --
```

are passed through the complete semantic and SQL pipeline.

The SQL compiler keeps them as bound parameters. The test executes the request against SQLite and PostgreSQL and verifies that the value does not widen the result set.

This is intentionally stronger than checking the generated SQL string alone.

### 3. Runtime authorization context isolation

Two requests with the same semantic shape but different runtime tenant contexts are executed through the same engine.

The provider compiler must run once.

The results must differ according to the runtime authorization context:

```text
same plan shape
      |
      +---- tenant 7 -> tenant 7 rows
      |
      +---- tenant 8 -> tenant 8 rows
```

This demonstrates that runtime authorization values are not frozen into the cached provider plan.

### 4. Authorization before compilation

A denied request is sent through the same engine.

The test asserts:

```text
authorization denied
        |
        +--> provider compiler: 0
        |
        +--> provider execution: 0
```

A cache hit therefore cannot be used as an authorization bypass because authorization occurs before provider-plan lookup.

### 5. PostgreSQL execution gate

When `FOUNDGINE_POSTGRES_CONNECTION_STRING` is configured, the same agent-shaped intents are executed against PostgreSQL.

The tests verify:

- tenant isolation at the actual database boundary
- parameterized hostile values
- execution evidence
- execution receipt

The PostgreSQL tests are skipped when the integration connection is not configured.

## Security invariants

M17.1 freezes the following invariants:

### I1 — Agent input is not authority

Structured intent cannot provide:

- identity
- tenant
- authorization
- provider
- SQL
- connection information

### I2 — Authorization precedes provider reuse

A request must pass semantic authorization before a cached provider plan can be used.

### I3 — Runtime authorization values remain runtime values

A provider plan may contain a provider-independent authorization predicate, but not a caller's concrete tenant value.

### I4 — User values are parameters

Agent-controlled scalar values must not become SQL syntax.

### I5 — Database execution preserves the semantic authorization boundary

The SQL provider must lower the authorization predicate into executable database constraints rather than trusting the agent request to have already been filtered correctly.

### I6 — Security failures are fail-closed

Malformed, over-limit, unauthorized, or execution-control-bearing intents fail before consequential execution.

## What M17.1 proves

M17.1 provides a substantially stronger claim than M17 alone:

> A hostile structured intent can be followed through the real Foundgine semantic, authorization, planning, caching, SQL compilation, and database execution pipeline without gaining execution authority or widening authorization scope.

It also demonstrates that provider-plan caching does not replace authorization.

## What it does not prove

M17.1 does not prove:

- natural-language interpretation is correct
- an LLM cannot produce an unsafe intent
- PostgreSQL database roles are configured securely
- network security is correct
- authentication is correct
- secrets are protected
- deployment infrastructure is secure
- every possible SQL/provider dialect is safe
- every possible domain authorization policy is correct

Those remain outside the semantic execution boundary.

## Validation

The source archive was structurally inspected and the M17.1 tests were added without introducing a parallel security or mutation subsystem.

The current execution environment does not contain the .NET SDK, so this milestone is **not represented as a passing `dotnet test` run**. The PostgreSQL tests are integration-gated and require:

```text
FOUNDGINE_POSTGRES_CONNECTION_STRING
```

## Next gate — M17.2

The next step should be **model-provider replay testing**, not another core feature.

M17.2 should take realistic hostile conversations and agent outputs and replay them as structured intents:

```text
prompt / conversation
        |
        v
model output fixture
        |
        v
structured intent
        |
        v
M17.1 black-box pipeline
        |
        v
security invariants
```

The important property is that M17.2 should not trust the model output merely because it is valid JSON. It should prove that prompt-injection-shaped requests remain bounded when converted into the canonical Foundgine intent contract.

After M17.2, the next major architecture gate should be a **Security Invariant Registry**: machine-readable invariants attached to capabilities, plans, providers, and receipts so the same security contract can be checked automatically rather than living primarily in test code and documentation.
