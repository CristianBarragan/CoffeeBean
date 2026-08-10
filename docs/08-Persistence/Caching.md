# Caching

Caching is not part of the current proven execution path.

If introduced later, it should sit around execution rather than changing semantic correctness.

Potential shape:

```text
Intent
 ↓
Plan
 ↓
Cache lookup
 ├── hit → result
 └── miss → provider execution
```

Cache invalidation must remain an application/provider concern.

Do not introduce caching before baseline execution benchmarks exist.
