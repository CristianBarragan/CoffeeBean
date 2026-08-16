# M18.15 — Test Fixes v3

This patch fixes the remaining implementation/fixture issues exposed by the latest E2E and planning test run.

## Fixes

- Aggregate semantic equivalence now recognizes `COUNT(R) > 0 AND SOME(R, P)` before individually normalizing the aggregate.
- Filtered COUNT normalization remains equivalent to SOME/NONE for the supported existence comparisons.
- `uint` is supported by the integral semantic fingerprint conversion without the invalid `uint <= long.MaxValue` comparison.
- Remaining E2E provider compiler fixtures explicitly declare the security-invariant preservation contract.

The production security proof gate remains strict. Tests were changed only to make their fake provider compilers declare the contract they already satisfy.
