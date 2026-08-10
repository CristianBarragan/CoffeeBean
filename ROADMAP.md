# Ground-Up Roadmap

- [ ] M1 — Semantic Foundation: IDs, metadata, semantic node/edge/graph.
- [ ] M2 — Resolution: semantic request -> request graph.
- [ ] M3 — Authorization: request graph -> authorized graph.
- [ ] M4 — Planning: authorized graph -> execution plan.
- [ ] M5 — SQL execution: execution plan -> provider plan -> SQLite.
- [ ] M6 — AOT: domain model -> generated metadata.
- [ ] M7 — GraphQL adapter: Hot Chocolate -> semantic request.

## First acceptance test

Customer -> Account -> Transaction must resolve into a semantic graph, plan without SQL knowledge, and eventually execute against a real database.
