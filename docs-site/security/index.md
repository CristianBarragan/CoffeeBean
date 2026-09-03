# Foundgine Security

Foundgine treats intent as untrusted and carries authorization constraints into planning and provider execution. Authentication and identity lifecycle remain host-owned.

## Invariant

![PlantUML diagram: index, diagram 1](assets/index-plantuml-01.svg)

Capability discovery is descriptive, not authorization. Caller-supplied claims cannot widen authority. Optional `Foundgine.Runtime.ControlPlane` infrastructure is outside the core execution boundary.

## Next

Read [AI agents](../ai-agents/index.html) next.
