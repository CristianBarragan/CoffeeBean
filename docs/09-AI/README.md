# AI Integration

Foundgine is AI-native at the application boundary, not an AI model framework.

## Core boundary

```text
LLM / Agent
     ↓
Structured Intent
     ↓
Foundgine
     ├── Resolution
     ├── Planning
     ├── Execution
     └── Evidence
```

The model may propose:

```text
Customer = Ada Lovelace
Path = Accounts → Transactions
Order = Transaction.Id DESC
Limit = 5
```

Foundgine resolves and validates that structured request against application metadata and executes it through the normal planner/provider path.

## Important distinction

The LLM does **not** need to know that the physical schema contains:

```text
Account.CustomerId
Transaction.AccountId
```

It should work with the application's semantic vocabulary.

Likewise, Foundgine should not teach the resolver to become a general natural-language parser. Language understanding belongs outside the core boundary.

## Current implementation

Implemented and proven:

- semantic model;
- metadata-backed semantic inference support;
- deterministic entity resolution;
- structured read intent;
- read planning;
- real SQLite end-to-end read proof;
- five-entity composite semantic/planning proof;
- repeated/self-joined entity proof.

Still to productize:

- reusable semantic → `QueryIntent` bridge;
- collection-aware relationship traversal in the reusable bridge;
- benchmark evidence.

Not yet complete:

- production LLM adapters;
- general intent extraction;
- semantic action lifecycle;
- production policy engine;
- MCP.
