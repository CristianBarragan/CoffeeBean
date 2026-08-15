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

## Current integration boundary

The shipped 0.3.0 integration boundary proves the Foundgine semantic lifecycle and MCP/mutation safety contracts. The full autonomous-agent caller loop remains a separate integration target:

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

> The semantic lifecycle itself is shipped and tested in 0.3.0. What remains outside the current core guarantee is a general autonomous-agent runtime that owns model selection, agent orchestration, deployment infrastructure, and end-to-end autonomous behavior.

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
