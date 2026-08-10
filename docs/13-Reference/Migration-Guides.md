# Migration Guides

## Historical Graphgine → Foundgine

The repository contains historical Graphgine/GraphQL work under `archive/`.

The active architecture is not a renamed GraphQL framework.

The current conceptual mapping is:

```text
Graphgine / GraphQL era
        ↓
historical archive

Foundgine current
        ↓
Semantic
Metadata
Planning
Execution
Providers
```

Do not move historical GraphQL projects back into the active dependency graph simply to preserve old APIs.

## Source generators

The historical generator is also archived.

Future Roslyn work should target the semantic domain compiler direction, not recreate the old GraphQL mapping generator.
