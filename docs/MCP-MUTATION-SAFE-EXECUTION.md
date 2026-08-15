# MCP Mutation Safe Execution

Foundgine MCP mutations are transport adapters over the semantic mutation pipeline. MCP never receives provider access or authorization authority.

## Flow

```text
MCP mutation request
  -> semantic mutation request
  -> semantic plan
  -> authorization
  -> dry-run
  -> plan fingerprint
  -> approval
  -> re-authorization
  -> exact fingerprint verification
  -> provider execution
```

## Tools

- `foundgine_mutation_dry_run`
- `foundgine_mutation_approve`
- `foundgine_mutation_execute_approved`

Execution is deliberately split from approval. An approval is bound to the exact authorized semantic mutation plan and cannot be used if the plan changes.

## Request shape

```json
{
  "operations": [
    {
      "entity": 1,
      "kind": "Update",
      "fields": { "2": "new value" },
      "filter": { "field": 1, "operator": "Eq", "value": 42 },
      "returnFields": [1, 2]
    }
  ]
}
```

Numeric IDs are intentional in this first adapter: MCP must not invent a second entity/field naming registry. Capability discovery remains the source of semantic identities.

## Security invariant

`foundgine_mutation_execute_approved` never trusts the approval as an authorization grant. The mutation is planned and authorized again immediately before execution; only an identical plan fingerprint can proceed.
