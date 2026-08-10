# Foundation

The foundation is the stable, protocol-neutral substrate.

It should not contain:

- GraphQL;
- SQL-specific logic;
- LLM dependencies;
- application-specific semantics.

## Main areas

```text
Foundgine.Abstractions
Foundgine.Foundation
Foundgine.Metadata
Foundgine.Builders
Foundgine.Execution.Contracts
```

## Design goal

The foundation should remain boring.

It provides the contracts and structures higher layers need without owning policy or transport decisions.
