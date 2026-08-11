# M18 — Mutation Dependencies

Ported from the archived mutation dependency proof.

The useful archived concept was the directed dependency edge between mutation rows. Foundgine expresses that as `MutationDependency` plus `MutationValueReference`.

The old CTE/graph-merge machinery is intentionally not ported.

Proof:

Customer -> generated Id -> Account.CustomerId -> generated Id -> Transaction.AccountId.

All three operations execute in one SQLite transaction and rollback together on failure.
