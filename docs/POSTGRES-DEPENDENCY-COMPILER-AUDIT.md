# PostgreSQL Compiler/Dependency References

- `tests/Foundgine.E2E.Tests/PostgresCorrelationContractTests.cs`
- `src/Foundgine.Sql/Mutation/PostgresBatchedMutationCompiler.cs`
  - line 234: `IReadOnlyList<MutationDependency> dependencies,`
  - line 840: `IReadOnlyList<MutationDependency> dependencies)`
- `src/Foundgine.Sql/Mutation/Postgres/PostgresBatchedMutationExecutionProvider.cs`
- `src/Foundgine.Planning/Mutation/MutationPlanner.cs`
  - line 115: `private static IReadOnlyList<MutationDependency> BuildSemanticDependencies(`
  - line 119: `var dependencies = new List<MutationDependency>();`
  - line 147: `dependencies.Add(new MutationDependency(`
  - line 174: `dependencies.Add(new MutationDependency(`
  - line 301: `/// only nesting *within* one item produces a MutationDependency - so no cross-item edges`
  - line 311: `var dependencies = new List<MutationDependency>();`
  - line 463: `private static IReadOnlyList<MutationDependency> BuildDependencies(`
  - line 466: `var dependencies = new List<MutationDependency>();`
  - line 487: `dependencies.Add(new MutationDependency(`
