# Foundgine Security

Foundgine treats intent as untrusted and carries authorization constraints into planning and provider execution. Authentication and identity lifecycle remain host-owned.

## Invariant

```text
Intent → Resolve → Authorize → Security-preserving Plan → Provider Conformance → Execute
```

Capability discovery is descriptive, not authorization. Caller-supplied claims cannot widen authority. Optional `Foundgine.Security.Authority` infrastructure is outside the core execution boundary.

## Next

Read [AI agents](../ai-agents/index.html) next.
