> Source content for [`index.html`](index.html), the page actually served on the site. Edit this file, then regenerate the HTML page and `llms-full.md`.

# AI Agents with Foundgine

## Safe AI access to application data

The purpose of the Foundgine AI integration is not to make the model responsible for database access.

The intended boundary is:

```text
AI Agent
   ↓
Tool / structured intent
   ↓
Foundgine
   ↓
Authorization + planning
   ↓
Provider
   ↓
PostgreSQL
```

## The anti-pattern

Avoid:

```text
LLM
 ↓
generated SQL
 ↓
database credentials
 ↓
PostgreSQL
```

The model should not be the authority over database schema access, tenant isolation, or application authorization.

## The Foundgine pattern

```text
LLM
 │
 │ "Find customers with balances over $10k"
 ▼
Agent tool
 │
 │ structured request
 ▼
Foundgine
 ├── semantic resolution
 ├── validation
 ├── authorization
 ├── relationship traversal
 ├── planning
 └── provider execution
        │
        ▼
    PostgreSQL
```

## End-to-end proof target

The first E2E scenario should prove the complete chain:

1. An AI agent receives a natural-language task.
2. The model selects a Foundgine capability.
3. The capability produces structured intent.
4. Foundgine resolves the semantic model.
5. Authorization is evaluated.
6. An execution plan is produced.
7. PostgreSQL executes the plan.
8. The result returns to the agent.
9. Evidence is available for inspection.

## What this page describes vs. what exists today

> Steps 3–7 above — structured intent, semantic resolution, authorization, planning, and provider execution — are the same core pipeline documented in [Architecture](../architecture/index.html); that pipeline is not specific to AI agents.
>
> What's specific to this page, and still a proof target rather than a shipped guarantee, is the full chain end to end for an AI agent caller: steps 1–2 and 8–9, the four security scenarios below, and the deployment progression from a local process through containerized and cloud infrastructure. Treat everything on this page as the architecture Foundgine is being built toward for AI-agent callers, and check the repository's releases for current implementation status before depending on it.

## Security scenarios

The E2E suite should include at least:

### Allowed field

```text
Agent → customer.name
→ authorized
→ SQL
→ result
```

### Forbidden field

```text
Agent → customer.internalRiskScore
→ denied
→ no database execution
```

### Tenant isolation

```text
Agent → another tenant's customer
→ authorization predicate prevents access
```

### Prompt injection

A malicious value in application data must not become an instruction that changes the agent's authorization or Foundgine execution boundary.

## Deployment progression

Build the E2E in this order:

```text
1. Agent process
2. Foundgine
3. PostgreSQL
4. Deterministic E2E
5. HTTP boundary
6. Docker
7. Kubernetes
8. Terraform
9. CI/CD
```

The local deterministic test should prove the architecture before infrastructure is introduced.
