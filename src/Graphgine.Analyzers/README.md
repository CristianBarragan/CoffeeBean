# Graphgine.Analyzers (placeholder)

No analyzers exist yet in the current codebase — `MappingDiagnostics.cs` in
`Graphgine.SourceGenerators` reports diagnostics from *inside* the source generator,
which is a different mechanism from a standalone `DiagnosticAnalyzer`.

This project exists so the solution shape matches the intended architecture, and
is where a real `DiagnosticAnalyzer` (with code fixes) for Graphgine-specific rules
should go once you have some.
