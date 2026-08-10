# Security Policy

Foundgine is an active proof project and does not currently claim production security certification or a complete authorization framework.

## Current safety properties

The active code already demonstrates some useful constraints:

- ambiguous entity resolution is not silently accepted;
- the mutation planner rejects unfiltered update operations;
- logical planning is separated from provider execution;
- execution is explicit through provider contracts.

## Future security work

The AI-facing security path still needs:

```text
identity resolution
 → authorization
 → action constraints
 → preview
 → execution
 → verification
 → evidence
```

## Reporting

For a suspected vulnerability, use the repository's private security reporting mechanism where available. Do not publish sensitive exploit details in a public issue.
