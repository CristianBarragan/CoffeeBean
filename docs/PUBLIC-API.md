# Public API

Foundgine exposes a deliberately small application-facing facade:

```csharp
var engine = new FoundgineEngine(
    model,
    authorizationPolicy,
    planner,
    providerCompiler,
    executionProvider);

var result = await engine.ExecuteAsync(request, context);
```

The facade owns the pipeline:

```text
SemanticRequest
    ↓
Resolution
    ↓
Authorization
    ↓
Planning
    ↓
Provider compilation
    ↓
Execution
```

Applications and adapters should not need to manually orchestrate those steps.

## Boundary

The public facade does not depend on SQL, GraphQL, or a specific database provider.

Provider-specific setup remains outside the core:

```text
Application
    ↓
FoundgineEngine
    ↓
provider contract
    ↓
SQL / other provider
```

This keeps the internal semantic/planning architecture available without making it the application developer's daily API.

## What remains intentionally public

- `SemanticRequest` as the protocol-neutral request model.
- `ExecutionContext` for runtime values.
- `ExecutionResult` and `ExecutionEvidence` for results and verification.

Internal planning and provider contracts remain implementation boundaries.

## Future extensions

Claims/roles and cache options should be added to the execution boundary only after their invariants are proven. They should not leak into the semantic model.


## Dependency injection

The recommended application-facing registration is:

```csharp
services.AddFoundgine(options =>
{
    options.Model = semanticModel;
    options.AuthorizationPolicy = authorizationPolicy;
});
```

Provider adapters register the provider-neutral services separately:

```csharp
services.AddSingleton<IProviderPlanCompiler>(compiler);
services.AddSingleton<IExecutionProvider>(provider);
```

Application code consumes the stable `IFoundgine` contract:

```csharp
var foundgine = services.BuildServiceProvider().GetRequiredService<IFoundgine>();
var result = await foundgine.ExecuteAsync(request, context);
```

This keeps provider construction at the infrastructure edge while hiding
resolution, authorization, planning and provider compilation from normal
application code.

The lower-level orchestration constructor is internal and is reserved for
Foundgine adapters and tests.
