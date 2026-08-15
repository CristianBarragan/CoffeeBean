# Generated-key correlation contract

This stage freezes the generated-key correlation contract before modifying
the PostgreSQL SQL compiler.

No new SQL strategy is introduced here.

The goal is to prevent the physical batching implementation from becoming the
source of semantic correlation rules.

Next implementation work should add executable validation at the actual
`PostgresBatchedMutationCompiler` correlation boundary, using the repository's
concrete execution-mutation structures.
