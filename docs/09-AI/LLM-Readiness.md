# LLM Readiness

Foundgine is designed so an LLM can be a **producer of structured intent**, not a privileged execution engine.

## Recommended interaction

```text
User
 ↓
LLM / parser
 ↓
ReadIntent / action intent
 ↓
Foundgine
 ↓
resolution
 ↓
QueryIntent
 ↓
planning
 ↓
execution
```

## The LLM boundary

The model can express:

```text
Find Ada's five most recent transactions.
```

but the model should ultimately hand Foundgine a structured representation such as:

```text
Anchor = Customer("Ada Lovelace")
Path = Accounts → Transactions
Order = Transaction.Id DESC
Limit = 5
```

Foundgine then resolves the identity and discovers the physical joins from application metadata.

## Important constraint

Do not describe Foundgine as an LLM framework.

The core must remain usable without any model provider.

Do not teach the core resolver arbitrary English, pronoun parsing or fuzzy reasoning beyond the explicitly supported semantic search capabilities.

## Current accuracy

Implemented/proven:

- semantic model;
- deterministic resolution;
- structured read intent;
- read planning;
- real SQLite read acceptance path;
- composite and repeated-entity planning proofs.

Next:

- reusable semantic → query bridge;
- collection-aware traversal;
- benchmark evidence.

Not yet complete:

- production LLM adapters;
- general intent extraction;
- semantic action lifecycle;
- production policy engine;
- MCP.
