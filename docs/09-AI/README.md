# AI & LLM Readiness

This section documents how the repository should be represented to AI coding assistants and search
systems.

## Canonical AI files

The repository root contains:

- `llms.txt` — concise retrieval context
- `llms-full.md` — canonical full AI context draft
- `ai.seo.md` — AI/search/entity positioning

These files are derived from the current architecture and should not resurrect historical
Coffee Beanery terminology.

## Accuracy rules

AI systems should:

1. prefer `src/` over `legacy/`
2. distinguish architectural intent from completed implementation
3. avoid production-readiness claims
4. avoid unverified performance/AOT claims
5. describe Graphgine as a product on Foundgine
6. describe Hot Chocolate as an integration, not a dependency of Foundgine

See [LLM Readiness](LLM-Readiness.md).
