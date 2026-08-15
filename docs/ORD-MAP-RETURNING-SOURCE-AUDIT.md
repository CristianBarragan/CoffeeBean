# PostgreSQL ord_map / RETURNING audit — ord_map / RETURNING Audit

## Source inventory

- `tests/Foundgine.E2E.Tests/PostgresCorrelationContractTests.cs`
  - 11: `var method = typeof(PostgresBatchedMutationCompiler).GetMethod(`
  - 12: `nameof(PostgresBatchedMutationCompiler.Compile),`
  - 21: `var method = typeof(PostgresBatchedMutationCompiler).GetMethod(`
  - 22: `nameof(PostgresBatchedMutationCompiler.TryCompile),`
- `src/Foundgine.Sql/Mutation/SqlMutationExecutionProvider.cs`
  - 14: `/// Executes one compiled mutation through ADO.NET and materializes RETURNING`
- `src/Foundgine.Sql/Mutation/SqlMutationCompiler.cs`
  - 13: `/// Upsert uses INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING.`
  - 112: `// Important: PostgreSQL returns no row from RETURNING when the`
  - 114: `// relies on RETURNING for mutation identity/dependency materialization,`
  - 172: `AppendReturning(sb, entity, op.ReturnFields, out var returns);`
  - 193: `AppendReturning(sb, entity, op.ReturnFields, out var returns);`
  - 218: `AppendReturning(sb, entity, op.ReturnFields, out var returns);`
  - 234: `private void AppendReturning(`
  - 245: `sb.Append(" RETURNING ");`
- `src/Foundgine.Sql/Mutation/PostgresBatchedMutationCompiler.cs`
  - 23: `public sealed class PostgresBatchedMutationCompiler`
  - 27: `public PostgresBatchedMutationCompiler(IMetadataProvider metadata) =>`
  - 566: `sql.Append("\n  RETURNING ")`
  - 648: `if (isDelete && !rewritten.Contains(" RETURNING ", StringComparison.OrdinalIgnoreCase))`
  - 649: `rewritten += " RETURNING 1 AS \"__affected\"";`
- `src/Foundgine.Sql/Mutation/Postgres/PostgresBatchedMutationExecutionProvider.cs`
  - 14: `/// safely be represented by PostgresBatchedMutationCompiler. Otherwise it`
  - 24: `private readonly PostgresBatchedMutationCompiler _compiler;`
  - 38: `_compiler = new PostgresBatchedMutationCompiler(metadata);`
- `src/Foundgine.Planning/Mutation/MutationPlanner.cs`
  - 294: `/// whole batch can go through a single-round-trip execution path (PostgresBatchedMutationCompiler`
