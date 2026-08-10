# Product Direction

[Home](../../README.md) → **Direction**

## Product statement

> **Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

Foundgine is deliberately narrower than an AI framework.

It owns the application-domain boundary:

```text
Domain
 ↓
Semantic meaning
 ↓
Structured intent
 ↓
Resolution
 ↓
Policy
 ↓
Execution plan
 ↓
Execution
 ↓
Evidence
```

The external AI system owns language reasoning and conversation.

---

## Why this exists

A generic LLM can generate a plausible request.

It does not automatically know:

- which customer "Ada" means;
- whether a relationship exists;
- which data is authoritative;
- which action is legal;
- which operation should execute;
- what must be verified after a mutation.

Foundgine turns those application facts into an explicit executable vocabulary.

The application remains the source of truth.

---

## The core distinction

Foundgine is not:

```text
LLM → SQL
```

It is:

```text
LLM / application
       ↓
Structured semantic intent
       ↓
Foundgine
       ↓
Safe executable plan
```

That distinction is central.

---

## Product boundary

Foundgine owns:

- semantic domain descriptors;
- identity and relationship resolution;
- constrained intent;
- provider-neutral execution plans;
- explicit domain actions;
- policy-aware planning;
- execution contracts;
- verification/evidence primitives.

Foundgine does not own:

- model inference;
- generic agent orchestration;
- MCP protocol implementation;
- vector databases;
- workflow engines;
- message brokers;
- application hosting.

---

## Development strategy

Foundgine is being developed through proof milestones.

The rule is:

> **Complete a vertical slice before expanding the platform.**

The next proof is:

```text
Find Ada's last five transactions
        ↓
semantic intent
        ↓
resolution
        ↓
query planning
        ↓
real database
        ↓
evidence
```

Then, and only then, expand the same pattern to actions and policy.

---

## Long-term architecture

```text
                 Claude / ChatGPT / Cursor
                            │
                       MCP / API
                            │
                            ▼
                 ┌───────────────────┐
                 │ Foundgine Semantic│
                 │       API         │
                 └─────────┬─────────┘
                           │
                 ┌─────────▼─────────┐
                 │     Semantic      │
                 │     Intent        │
                 └─────────┬─────────┘
                           ▼
                       Resolve
                           ▼
                        Policy
                           ▼
                        Plan
                           ▼
                      Execute
                           ▼
                    Verify / Evidence
```

MCP is outside the core.

---

## What success looks like

The first strong product proof is not a benchmark, MCP demo or LLM demo.

It is a deterministic application-domain execution path that an AI can safely call:

```text
"Find Ada's last five transactions."

→ resolve Ada
→ traverse Accounts
→ query Transactions
→ order
→ limit
→ execute
→ return evidence
```

That is the foundation for the larger product.
