# End-to-end agent benchmark

The most interesting Foundgine question is not only **how fast can the provider execute a query?** It is:

> Can the same complex business process be completed with fewer agent/tool round trips and fewer model-context tokens when the agent works against a semantic capability instead of a physical application surface?

The benchmark compares two complete flows against the same PostgreSQL fixture and the same final-state assertion.

## The business process

Review a customer's lending exposure across relationships, contracts and transactions. If exposure is at least 48,000, mark the customer as reviewed and verify the final state.

```text
Customer
  └── CustomerBankingRelationship
        └── Contract
              └── Transaction
                    └── Balance
```

The important point is that both agents must reach the **same business outcome**. Token savings alone are not evidence if the result differs.

## Conventional flow

```text
User request
    ↓
LLM
    ↓
describe schema
    ↓
LLM
    ↓
find customer
    ↓
LLM
    ↓
list relationships
    ↓
LLM
    ↓
list contracts
    ↓
LLM
    ↓
list transactions
    ↓
LLM
    ↓
update customer
    ↓
LLM
    ↓
verify
    ↓
Final result
```

## Foundgine flow

```text
User request
    ↓
LLM
    ↓
semantic capability
    ↓
Foundgine
    ├── resolve intent
    ├── authorize semantic graph
    ├── plan traversal
    └── execute provider plan
    ↓
Business graph result
    ↓
LLM
    ↓
semantic mutation
    ↓
Foundgine
    ├── authorize
    ├── plan
    └── execute
    ↓
LLM
    ↓
verify
    ↓
Final result
```

## What matters

The benchmark records:

- input tokens
- output tokens
- total tokens
- cached input tokens
- model calls
- tool calls
- wall-clock time
- model time
- tool time
- final-state equivalence

The token counts come from provider-reported usage in live mode. The benchmark deliberately does not claim that character count divided by four is a token measurement.

## The claim to test

The benchmark should ultimately let the repository say something precise such as:

> Same request. Same PostgreSQL fixture. Same final state. Same model. **X% fewer input tokens, Y% fewer total tokens and Z fewer tool round trips.**

The values above are placeholders for measured results; they are not hard-coded claims.

See the runnable benchmark under `benchmarks/AgentEndToEnd`.
