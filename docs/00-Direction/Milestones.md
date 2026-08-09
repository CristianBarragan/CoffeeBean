# Foundgine Proof Milestones

[Home](../../README.md) → [Direction](README.md) → **Milestones**

This is the execution roadmap for proving the Foundgine thesis. It intentionally prioritizes one complete vertical slice over broad feature coverage.

## Milestone 0 — Real execution

**Goal:** prove the existing lower half works.

```text
Domain
→ Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ real database
→ Result
```

### Acceptance criteria

- Banking domain contains Customer, Account and Transaction.
- Metadata describes the entities and joins.
- Planner discovers the path from metadata rather than hardcoding banking joins.
- Provider compiler produces a SQL-shaped provider plan.
- SQL executes against a real database.
- Results are returned through `ExecutionRow`.
- No GraphQL or AI dependency exists in the sample.

**Status:** active proof exists in `samples/Foundgine.Samples.Banking`.

---

## Milestone 1 — Semantic domain

**Goal:** turn metadata into a semantic application model.

Add descriptors for:

```text
Entity
Identity
Field
Relationship
Search capability
Action
Policy
```

The semantic model must be protocol-neutral.

### Acceptance test

Given the Banking domain, Foundgine can enumerate:

```text
Customer
 ├── identity: Id
 ├── fields: Name
 ├── relationship: Accounts
 └── actions: <none initially>

Account
 ├── identity: Id
 ├── fields: Balance
 └── relationship: Transactions

Transaction
 ├── identity: Id
 └── fields: Amount
```

**Important:** start with hand-authored metadata if necessary. Do not block the product proof on a source generator.

---

## Milestone 2 — Semantic resolution

**Goal:** map ambiguous human language to domain identities.

Examples:

```text
"Ada Lovelace"
"account 10"
"her checking account"
"the last transaction"
```

into explicit domain references.

### Acceptance criteria

The resolver returns:

- entity type
- identity
- confidence/reason
- evidence used
- unresolved ambiguity when confidence is insufficient

The resolver must never silently invent an identity.

---

## Milestone 3 — Read intent

**Goal:** prove natural-language-to-read-plan without building a generic AI framework.

```text
Natural language
      ↓
Intent
      ↓
Resolve
      ↓
Semantic query
      ↓
Foundgine QueryPlan
      ↓
ProviderPlan
      ↓
Database
      ↓
Evidence
```

The LLM is an optional reasoning client. Foundgine owns the constrained semantic representation and execution.

### Acceptance example

> Find Ada's last five transactions.

Expected plan:

```text
Resolve Customer
→ Resolve Account through Customer relationship
→ Query Transaction ordered by transaction identity/time
→ Limit 5
```

---

## Milestone 4 — Domain actions

**Goal:** expose explicit business operations rather than arbitrary method invocation.

Example:

```text
IssueRefund(amount)
SuspendAccount(reason)
ChangeTier(tier)
```

Each action gets a descriptor:

```text
Name
Target entity
Inputs
Mutating?
Authorization requirements
Side effects
Verification requirements
```

### Safety rule

An agent can select only actions exposed by the semantic model.

It cannot invent arbitrary C# method calls.

---

## Milestone 5 — Policy / authorization

**Goal:** authorization becomes part of planning.

```text
Intent
 ↓
Resolve
 ↓
Policy
 ↓
Plan
```

Example:

```text
IssueRefund
requires:
  Refund permission
  Customer ownership
  amount <= configured limit
```

Denied plans should be explicit and explainable.

---

## Milestone 6 — Preview and approval

**Goal:** mutations are inspectable before execution.

```text
PLAN
 ↓
PREVIEW
 ↓
APPROVE
 ↓
EXECUTE
```

A preview should identify:

- target
- action
- inputs
- expected changes
- policy decision
- likely side effects
- verification strategy

This is a core safety mechanism, not a UI feature.

---

## Milestone 7 — Verification and evidence

**Goal:** every important execution can prove what happened.

```text
Execute
 ↓
Re-read affected state
 ↓
Verify expected result
 ↓
Produce evidence
```

Evidence should capture enough information to explain:

```text
What was resolved?
Why was it selected?
What policy was applied?
What plan ran?
What provider executed?
What changed?
How was the result verified?
```

---

## Milestone 8 — MCP adapter

**Goal:** expose the proven semantic API to external agents.

MCP is an adapter, not Foundgine's core.

```text
Claude / ChatGPT / Cursor
           ↓
          MCP
           ↓
Foundgine Semantic API
           ↓
Foundgine Runtime
```

Start with a minimal tool surface:

```text
discover
resolve
query / plan
preview
execute
verify / evidence
```

Do not create dozens of entity-specific MCP tools.

---

## Milestone 9 — Additional execution targets

Only after the first vertical slice works:

```text
Structured data
Domain actions
Semantic retrieval
External data
```

These are execution targets behind a common planning model.

Do not build all four at once.

---

## Milestone 10 — Compile-time domain compiler

Only after the semantic model and runtime are proven should Roslyn generation become a major focus.

The compiler should generate:

- stable identifiers
- entity descriptors
- relationship descriptors
- search descriptors
- action descriptors
- policy metadata
- planner hints

It should **not** generate a fixed planner for future natural-language requests.

Dynamic intent remains a runtime concern.

---

# Definition of done for the first real proof

The first convincing Foundgine demo is one test that can execute:

```text
"Find Ada's checking account."
        ↓
resolve Customer
        ↓
resolve Account
        ↓
policy check
        ↓
plan
        ↓
execute
        ↓
evidence
```

and:

```text
"Refund Ada's last transaction."
        ↓
resolve Customer
        ↓
resolve Transaction
        ↓
authorize IssueRefund
        ↓
build mutation plan
        ↓
preview
        ↓
approve
        ↓
execute
        ↓
verify
        ↓
evidence
```

If these work against a real database and real domain action, the thesis has been proven.

Everything else is expansion.
