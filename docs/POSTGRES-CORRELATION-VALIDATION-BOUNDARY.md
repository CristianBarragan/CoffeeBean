# PostgreSQL correlation validation boundary

This stage establishes the validation contract immediately before
PostgreSQL SQL generation.

The purpose is to make correlation correctness an explicit compiler invariant,
rather than relying on SQL shape or result ordering.

No provider-specific optimization is introduced here.

The next implementation step is to bind these validations to the concrete
correlation structures used by `PostgresBatchedMutationCompiler`.
