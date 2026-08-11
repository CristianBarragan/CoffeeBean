# Authorization

Foundgine treats authorization as part of semantic execution rather than as a
transport-specific check around execution.

The model has four important boundaries:

```text
Entity access
    ↓
Field access
    ↓
Relationship access
    ↓
Conditional predicate
```

A policy can therefore describe a domain such as:

```text
Employee
 ├── readable
 ├── writable
 └── fields
      ├── Name       read/write
      ├── Email      read/write
      ├── Salary     denied
      └── TenantId   conditional
```

## Conditional authorization

Conditional access is represented by the small provider-independent predicate
IR in `Foundgine.Abstractions`.

For example:

```csharp
Expression<Func<UserContext, Employee, bool>> CanReadEmployee =>
    (user, employee) => user.TenantId == employee.TenantId;
```

The generator/policy layer can represent the condition as:

```text
Equal
├── MemberAccess(resource, TenantId)
└── MemberAccess(context, TenantId)
```

The expression tree is not retained and is never compiled or invoked by the
runtime. The predicate travels with the semantic graph into the
provider-independent execution plan.

Providers lower that predicate into their native representation. The SQL
provider, for example, turns the resource member into a storage column and
binds the context member as a runtime parameter.

## Capability discovery

Foundgine also exposes a provider-independent capability description through
`DescribeCapabilities()`.

This is intended for callers that need to understand what they can ask for,
including AI agents:

```text
Claims / Roles / Application identity
                ↓
        Authorization policy
                ↓
        Semantic capabilities
                ↓
       Agent builds valid intent
                ↓
        Authorization again
                ↓
        Execution plan
```

Capability discovery is **descriptive, not authoritative**. An agent or API
caller must never be trusted because it previously received a capability
snapshot. The execution pipeline evaluates authorization again before a plan
is produced.

The capability model reports `Denied`, `Allowed`, or `Conditional` for entity,
field, and relationship access. Policy implementation details are not exposed
as a requirement for the caller.

## Write authorization

Write access is explicitly opt-in. Existing read-only policies do not
accidentally become write-enabled.

Mutation authorization checks:

- entity write permission;
- field write permission for supplied values;
- field read permission for returned fields;
- read permission for fields and relationships used by mutation filters.

`MutationPlanner` remains structural. `MutationAuthorizer` applies the
semantic policy after planning and before provider compilation.

## Caching boundary

Authorization predicates must remain part of execution semantics.

A future plan cache may safely reuse a plan shape, but it must not turn:

```text
resource.TenantId == context.TenantId
```

into an authorization-free cached plan.

The intended model is:

```text
cacheable plan shape
        +
retained authorization predicate
        +
current execution context
        ↓
safe provider execution
```

Claims, roles, identity providers, and policy administration are deliberately
outside this layer. They can sit above Foundgine and produce semantic policy
decisions without becoming part of the Foundgine core.
