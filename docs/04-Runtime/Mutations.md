# Mutations

The lower runtime contains a provider-neutral mutation model:

```text
MutationIntent
   ↓
MutationPlanner
   ↓
MutationPlan
   ↓
ProviderMutationPlan
   ↓
Provider execution
```

## Currently proven

Create, update and delete are proven against SQLite.

The planner also rejects an unfiltered update so a caller cannot accidentally update every row.

Multiple entity mutations can be submitted as one provider mutation plan and committed atomically in the SQLite proof.

## Not yet complete

The following remain incomplete in the SQL provider:

- Upsert;
- GraphMutation;
- RelationshipMutation;
- generated mutation values;
- expression-based mutation values.

## Semantic mutations are later

The semantic action lifecycle is a separate layer:

```text
Action intent
 → resolve
 → authorize
 → mutation plan
 → preview
 → execute
 → verify
```

Do not confuse the existing low-level mutation planner with the future AI-facing action system.
