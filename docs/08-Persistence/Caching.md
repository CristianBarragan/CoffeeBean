[Home](../../README.md) → [Documentation](../README.md) → [Persistence](README.md) → **Caching**

# Caching

## Contents

- [Startup warmup](#startup-warmup)
- [In-process caching](#in-process-caching)
- [What warmup produces](#what-warmup-produces)

---

## Startup warmup

Before the first request is served, `GraphWarmup.Init` runs a warmup pipeline:

1. **Mapping set discovery** — scans the assembly for every `IMappingSet` implementation and
   registers it against both the model-type and entity-type axes.
2. **Property cache population** — `MappingWarmup.WarmupMap` walks every `FieldMap` and stores
   resolved `PropertyInfo` objects in `NodeMap.ModelProperties` / `NodeMap.EntityProperties`,
   eliminating per-request `Type.GetProperty` calls.
3. **Delegate compilation** — `BulkMapper.Compile` builds `Expression`-based getter/setter
   delegates, compiled to IL via `Expression.Lambda.Compile()`, cached in a
   `ConcurrentDictionary` keyed by `TypeFullName.PropertyName`.
4. **NodeTree generation** — `NodeTreeIterator.GenerateTree` pre-builds the full traversal
   tree for every root mapping.

By the time the first request arrives, the mapping layer has no reflection work left.

## In-process caching

The runtime's `CacheHelper` (`Cache/CacheHelper.cs`) provides in-process caching backed by
`FasterKv.Cache.Core` — no external cache server is required to run the sample locally. This
is separate from [future infrastructure providers](../02-Architecture/Vision.md#roadmap-by-phase)
like Redis, which are roadmap items for distributed caching, not what's used today.

## What warmup produces

Three `ConcurrentDictionary` caches — `_propCache`, `_getterCache`, `_setterCache` — populated
once at startup and read on every request afterward. See
[Performance → Benchmarks](../10-Performance/Benchmarks.md) for the measured effect.

---

## Related Documentation

- [Dapper & EF Core](Dapper-EFCore.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)
- [Runtime → Execution](../04-Runtime/Execution.md)

---

← Previous: [Dapper & EF Core](Dapper-EFCore.md)  |  Next: [AI & LLM Readiness](../09-AI/README.md) →
