[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **Migration Guides**

# Migration Guides

## Contents

- [No versioned releases yet](#no-versioned-releases-yet)
- [Adopting the mapping generator](#adopting-the-mapping-generator)

---

## No versioned releases yet

As noted in the [Changelog](Changelog.md), there's no tagged release history yet, so there
are no version-to-version migration guides in the traditional sense. This page will grow one
entry per breaking change once releases start shipping.

## Adopting the mapping generator

The closest thing to a migration guide that exists today is the process for moving an
existing, hand-written mapping project onto the
[source generator](../06-Source-Generators/Mapping-Generator.md) — this is itself a form of
migration (from `NodeBuilder<TContext>`'s five reflective passes to compile-time generation),
and it's the one documented in detail:

1. Make every mapping class `partial`.
2. Make `BaseModelMappingRegistration<T>.Register()` `virtual`.
3. Expose the mapping constructor's alias/model-name as `protected` properties.
4. Reference the generator project as an `Analyzer`, not a normal project reference.
5. Drop the `NodeBuilder<TContext>.BuildFromMappings()` call from startup — registration now
   happens per-instance via each mapping class's generated `Register()` override.

See [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md#required-changes-to-existing-hand-written-code)
for the full, exact steps, including how ambiguous navigations are handled differently
(a build-time `CBMAP003` diagnostic instead of a runtime exception).

---

## Related Documentation

- [Changelog](Changelog.md)
- [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md)
- [Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md)

---

← Previous: [Changelog](Changelog.md)  |  Next: [Documentation Home](../README.md) →
