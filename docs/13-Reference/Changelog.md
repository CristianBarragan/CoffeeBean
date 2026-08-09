# Changelog

## Unreleased — AI-native direction

Documentation has been realigned around the current Foundgine product thesis:

> **Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

### Documentation changes

- Added `docs/00-Direction/` with the product boundary and proof milestones.
- Reworked the root README around semantic execution rather than GraphQL.
- Updated architecture documentation to match the active `src/` project graph.
- Updated the Banking sample documentation as the canonical E2E proof.
- Reworked AI/LLM documentation and `llms.txt`.
- Replaced stale AI/search positioning in `ai.seo.md`.
- Marked GraphQL and the former source-generator architecture as historical.
- Added an explicit M0–M10 milestone chain.
- Added accuracy rules separating implemented, in-progress, planned and historical capabilities.

### Product status

The active Banking sample proves:

```text
Domain
→ Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ real SQLite database
→ Result
```

The next product work is:

```text
Semantic domain
→ Resolution
→ Read intent
→ Domain actions
→ Policy
→ Preview
→ Verification
→ Evidence
→ MCP
```
