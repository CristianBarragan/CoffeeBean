# Foundgine Semantic Algebra Decisions

## Decision

Foundgine treats the existing semantic model as the foundation of a typed semantic algebra, but does not replace the established query/mutation APIs in one breaking rewrite.

The common abstraction is `SemanticExpression`. Query filters are boolean-valued semantic expressions, ordering can be represented by ordering a semantic expression, and aggregates are expressions rather than special provider operations.

## What was merged

- Provider-neutral `SemanticType` remains the semantic type contract; `ClrType` is retained only as an adapter compatibility bridge.
- `SemanticValue` provides a canonical semantic value representation while existing `object?` constructors remain compatible.
- `SemanticExpression` provides a common compositional calculus for literals, field/relationship references, unary/binary/logical expressions, aggregates and functions.
- Semantic expression normalization is deterministic and conservative. It flattens associative AND/OR trees, removes logical identity elements, deduplicates equivalent children, and orders commutative children canonically.
- Canonical semantic expression serialization and SHA-256 hashing are available for future operation caching/fingerprinting.
- Ordering gains general expression forms while `SemanticOrderTerm` remains for compatibility.
- Read IR can distinguish output fields from internal required fields through `RequiredFields`.

## What is deliberately not merged yet

### Provider-specific optimization into Semantics

Predicate pushdown, join strategy, index selection and SQL-specific rewrites remain planning concerns. The semantic layer can define equivalence and safety contracts without becoming a second query optimizer.

### Full breaking replacement of filter/order records

Existing GraphQL, MCP, AOT and SQL adapters already consume the compatibility records. Replacing them immediately would create unnecessary API churn without improving semantic correctness enough to justify it.

### Arbitrary expression evaluation

The expression algebra describes semantic meaning. It does not execute arbitrary functions or permit providers to smuggle implementation-specific operations into the semantic layer.

### Authorization after unrestricted optimization

Authorization remains a security boundary. Any future optimizer must prove that a rewrite preserves authorization predicates and cannot increase authority.

## Target pipeline

```text
Intent
  -> Resolve
  -> Validate
  -> Normalize
  -> Authorization
  -> Security-preserving Optimize
  -> Canonical Semantic IR
  -> Provider Plan
  -> Execute
```

The key invariant is:

> A semantic rewrite may change representation, but it must preserve meaning and must never increase authority.

## Next recommended step

Before adding more algebraic features, integrate canonical operation hashing at the `SemanticOperation` boundary and add equivalence/property tests for every future rewrite. Then use those contracts to drive optimizer work in the planning layer.
