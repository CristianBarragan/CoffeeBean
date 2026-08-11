# Authorization

Foundgine authorization is expressed as ordinary C# expression trees at the
application boundary and reduced to a small AOT predicate IR.

```csharp
Expression<Func<UserContext, Contract, bool>> CanVisitContract =>
    (user, contract) => user.TenantId == contract.TenantId;
```

The generator produces an `AuthorizationPredicate` tree:

```text
Equal
├── MemberAccess(user, TenantId)
└── MemberAccess(contract, TenantId)
```

The expression tree is not retained and is never compiled or invoked at
runtime. The predicate travels with the semantic connection into the
provider-independent execution plan. Providers can then lower the predicate
to their native representation and bind context values.

The initial IR intentionally supports only simple, analyzable operations:
parameters, member access, constants, equality/inequality, boolean AND/OR,
and NOT. More operations should be added only when there is a clear semantic
and provider-independent representation.
