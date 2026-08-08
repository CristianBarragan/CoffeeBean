# Foundgine.Reflection (placeholder)

This project is part of the target Foundgine layout but has no extracted content yet.

Today, expression-tree/reflection helpers (e.g. `ExpressionHelper` — pulling a member
name out of a `m => m.Prop` lambda) live in `Graphgine/Mapping` because the current
mapping code is still entangled with GraphQL/entity concerns. Extracting a truly
generic version into this project (no dependency on `Graphgine.*`) is a good follow-up
once there's a second consumer of the platform to validate the abstraction against.

Until then, this project exists so the solution shape matches the intended
architecture, and so future reflection-related platform code has an obvious home.
