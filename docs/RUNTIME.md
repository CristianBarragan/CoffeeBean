# Runtime

The Foundgine runtime coordinates semantic requests, planning, provider execution, and results.

## Reads

A normal read follows:

```text
ReadIntent
   ↓
semantic resolution
   ↓
validation / normalization
   ↓
authorization
   ↓
semantic plan
   ↓
ExecutionIR
   ↓
provider compilation
   ↓
provider execution
   ↓
result materialization
```

`IFoundgine.ExecuteAsync(...)` is the application-facing boundary.

## Execution context

Execution is request-scoped.

The context may contain:

- security information;
- authorization values;
- provider/request values;
- runtime controls.

Authority-bearing values must originate from the host.

## Provider boundary

`Foundgine.Execution` separates logical plans from provider plans:

```text
ExecutionIR
   ↓
IProviderPlanCompiler
   ↓
ProviderPlan
   ↓
IExecutionProvider
```

This is the point where SQL or another physical representation is allowed.

## Plan caching

A provider plan cache can sit around compilation.

The safe order is:

```text
resolve
 ↓
authorize
 ↓
cache/compile
 ↓
execute with current context
```

The cache must not remove runtime authorization predicates.

## Security conformance

Before execution, required security invariants are compared with provider guarantees.

```text
required invariants
       ↓
provider conformance
       ↓
satisfied?
  ├── yes → execute
  └── no  → reject
```

This protects the semantic contract from a provider that cannot preserve it.

## Results

The runtime distinguishes provider execution from result materialization.

Typical flow:

```text
provider rows
     ↓
MaterializedResult
     ↓
ExecutionResult
     ↓
application / adapter
```

Adapters such as GraphQL can then shape the result for their own transport.

## Evidence

Execution evidence/receipts can record the execution outcome and relevant security/plan context.

Evidence is diagnostic/audit information. It is not an authorization grant.

## Mutations

Mutation runtime is intentionally separate from reads.

```text
SemanticMutationOperationGraph
          ↓
MutationPlan
          ↓
dependency levels
          ↓
security/conformance checks
          ↓
provider execution
          ↓
MutationResult
```

Generated-value dependencies are represented explicitly.

## Cancellation and resource limits

Execution APIs accept cancellation tokens.

Untrusted request complexity is bounded by semantic/security resource limits before expensive provider execution.

Application-level rate limits, quotas, and timeouts remain necessary around Foundgine.

## Runtime non-goals

The runtime does not own:

- authentication;
- user sessions;
- database migrations;
- ORM change tracking;
- LLM orchestration;
- GraphQL hosting;
- MCP transport hosting.

## Custom provider checklist

A provider implementation should prove:

- plan/IR compilation;
- provider execution;
- result materialization;
- cancellation behavior;
- security conformance;
- authorization predicate preservation;
- pagination semantics;
- mutation dependencies if mutations are supported.

See `Foundgine.Execution/README.md` and `Foundgine.Sql/README.md` for the provider boundary.
