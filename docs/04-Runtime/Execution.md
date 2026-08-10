# Execution

Execution is provider-independent at the contract boundary.

```text
ProviderPlan
   ↓
IExecutionProvider
   ↓
ExecutionResult / ExecutionRow
```

## ExecutionRow

Rows are occurrence-aware.

This matters when a plan contains repeated entities:

```text
Employee
   ↓
Manager
   ↓
Manager
```

An `EntityId` alone is not sufficient to identify both Manager occurrences.

The current contract therefore preserves:

```text
EntityId
OccurrenceIndex
Values
```

`Single(EntityId)` is appropriate only when the entity occurs once.

`All(EntityId)` returns all occurrences.

## Statistics

Execution statistics should remain provider-neutral and should not expose SQL-specific internals unless the provider adds them explicitly.
