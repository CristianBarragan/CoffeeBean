# Lifetimes

Recommended defaults are:

| Component | Typical lifetime |
|---|---|
| Metadata registry | Singleton |
| Semantic model | Singleton |
| Join graph | Singleton |
| Planner | Singleton/stateless |
| Resolver | Scoped or singleton depending on candidate source |
| Execution provider | Singleton if stateless |
| Execution context | Per execution |
| Evidence | Per execution |

These are guidelines, not hard framework rules.

The important rule is that execution-specific state must not leak between requests.
