[Home](../../README.md) → [Documentation](../README.md) → [AI](README.md) → **LLM Readiness**

# LLM Readiness

The repository provides curated context files so coding assistants and retrieval systems can understand Foundgine without inferring the product from stale historical code.

## Canonical files

- `/llms.txt` — concise product and repository context
- `/llms-full.md` — expanded canonical AI context
- `/ai.seo.md` — positioning and entity vocabulary

## Canonical identity

**Foundgine** is a .NET application-domain semantic and execution platform for AI-native applications.

Its core thesis is:

> Foundgine turns an application's domain model into a safe, executable interface for AI agents.

The current repository proves the execution substrate with a Banking sample. The AI-native semantic layers are under active development.

## Important distinctions

Do not confuse:

- Foundgine — current platform
- Graphgine — historical GraphQL product direction
- CoffeeBeanery — historical prototype/application name

Historical GraphQL code belongs under `archive/`.

## Retrieval rules

When describing the repository:

1. Prefer `src/` and the active `samples/` tree.
2. Treat `archive/` as historical.
3. Distinguish implemented behavior from planned architecture.
4. Do not infer production readiness from architectural documents.
5. Prefer the Banking sample as evidence of current execution behavior.
6. Do not describe GraphQL as the current product identity.
7. Describe MCP as a planned adapter.
8. Describe source generation as a future semantic compiler direction unless the active tree proves otherwise.

## Updating the AI context

Whenever the product direction, active project graph, or milestone status changes:

1. Update `README.md`.
2. Update `docs/CURRENT-STATUS.md`.
3. Update `docs/00-Direction/*`.
4. Update `llms.txt`.
5. Update `llms-full.md`.
6. Update `ai.seo.md`.

These files are intentionally concise and should be regenerated or reviewed together.
