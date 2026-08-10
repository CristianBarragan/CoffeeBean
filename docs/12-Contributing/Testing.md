# Testing

Testing mirrors the architecture.

## Unit tests

Each active project has focused tests.

## Architecture tests

`ArchitectureTests` checks project-reference direction.

## E2E tests

The important execution claims are proven against real SQLite.

Current E2E coverage includes:

```text
BankingEndToEndTests
UglySchemaEndToEndTests
ProductCompositeEndToEndTests
RepeatedEntityEndToEndTests
FilterSortPageEndToEndTests
MutationEndToEndTests
ReadIntentEndToEndTests
ProductSemanticIntentEndToEndTests
```

The semantic/read proofs intentionally build structured intent rather than parsing natural language. That keeps the test focused on Foundgine's boundary rather than pretending it is an LLM.

## Semantic tests

`Foundgine.Semantic.Tests` covers the semantic model, inference, resolution, read planning and experimental action/policy descriptors.

## Important acceptance invariant

Resolution must never silently invent an identity.

A second invariant is equally important for the next milestone:

```text
identity resolution ≠ collection traversal
```

A one-to-many relationship must be allowed to produce a query branch/set rather than forcing the test to choose one arbitrary child.

## Testing rule

If the claim is:

> "This provider executes correctly."

use a real provider/database.

If the claim is:

> "This planner rejects invalid metadata."

a focused unit test is appropriate.

## Future

Add benchmark tests separately from correctness tests.

Do not use benchmarks as correctness assertions.
