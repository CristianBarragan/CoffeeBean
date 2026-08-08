# Source Generators

`Graphgine.SourceGenerators` is the Roslyn source-generator project behind the compile-time-oriented
architecture.

## Purpose

The generator reads mapping information during compilation and emits strongly typed runtime
artifacts such as:

- stable identifiers
- metadata
- planner support
- materializers
- mutation-related generated structures
- mapping-derived adapter support

The goal is to avoid repeatedly rediscovering application structure at request time.

## Generator versus analyzer

These are intentionally separate:

```text
Graphgine.SourceGenerators → generates code
Graphgine.Analyzers         → reports diagnostics
```

`Graphgine.Analyzers` is currently a placeholder project.

## Important accuracy note

Do not describe the current framework as absolutely reflection-free or fully Native AOT verified.
The compile-time-first design strongly reduces runtime discovery, but the repository still contains
runtime and integration work that must be validated before making absolute claims.

## Pipeline

See:

- [Mapping Generator](Mapping-Generator.md)
- [Pipeline Stages](Pipeline-Stages.md)
- [Diagnostics](Diagnostics.md)
