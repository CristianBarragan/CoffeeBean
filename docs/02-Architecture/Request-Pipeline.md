[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Request Pipeline**

# Request Pipeline

The target Foundgine pipeline separates compiled application knowledge from dynamic runtime intent.

## 1. Domain compilation

```text
C# domain
   ↓
semantic descriptors
```

The descriptors identify what the application can expose.

## 2. Agent intent

An external agent or application produces an intent such as:

```text
Find Ada's last five transactions.
```

Foundgine does not need to own the LLM that produced the intent.

## 3. Resolution

```text
Ada
 ↓
Customer #1
 ↓
Account #10
 ↓
Transactions
```

Resolution should record why each identity was selected.

## 4. Policy

Before an action or protected query proceeds:

```text
Intent
 ↓
Resolved targets
 ↓
Policy evaluation
```

A denial is a valid execution result.

## 5. Planning

The planner converts the resolved intent into explicit execution structures:

```text
QueryIntent
    ↓
QueryPlan
    ↓
ProviderPlan
```

For mutations:

```text
ActionIntent
    ↓
MutationPlan
```

## 6. Preview

Mutating plans should be renderable before execution:

```text
Target
Action
Inputs
Policy result
Expected effects
Verification strategy
```

## 7. Execute

Providers execute the approved plan.

The provider boundary is represented by `Foundgine.Execution.Contracts`.

## 8. Verify

After a mutation, Foundgine should re-read or otherwise validate the affected state.

Verification is part of the product contract, not merely a test concern.

## 9. Evidence

The result should contain an evidence chain sufficient to answer:

```text
Who/what was selected?
Why?
What policy was applied?
What plan ran?
What actually happened?
How was it verified?
```

## 10. AI response

The external AI system can turn the evidence into a natural-language response.

Foundgine does not need to generate the prose itself.

## Current implementation

The Banking sample currently proves:

```text
Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ real SQLite
→ Result
```

Steps involving natural-language intent, policy, actions, preview, verification and evidence are roadmap work.

## Design principle

**The AI decides what it wants; Foundgine decides what the application permits and how that intent can execute.**
