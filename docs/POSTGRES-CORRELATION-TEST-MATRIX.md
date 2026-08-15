# PostgreSQL Correlation Test Matrix

The PostgreSQL mutation compiler should eventually have explicit tests for:

| Case | Expected |
|---|---|
| One generated key, one consumer | batch |
| One generated key, multiple consumers | batch |
| Missing source operation | reject/fallback |
| Missing returned field | reject/fallback |
| Invalid source ordinal | reject/fallback |
| Dependency before source | reorder or reject |
| Cross-group reference with no mapping | reject/fallback |
| Independent groups | batch independently |
| Duplicate literal conflict key | fallback/sequential |
| Duplicate generated conflict identity | provider capability decides; never silently collapse |
| Returned rows fewer than logical operations | execution/correlation failure |
| Returned rows more than logical operations | execution/correlation failure |

The test suite should distinguish:

1. semantic invalidity,
2. unsupported physical batching,
3. provider execution failure,
4. unknown outcome after execution.

These must not be represented as the same failure.
