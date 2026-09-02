# Foundgine v2 structure validation

This repository snapshot was statically validated after the v2 package-boundary and public-API cleanup.

## Applied boundary rules

- `Foundgine.Core` owns provider-neutral semantic, planning, IR, and execution contracts.
- Provider-neutral execution contracts use `Foundgine.Core.Execution`.
- `Foundgine.Runtime` owns application orchestration, approvals, mutation execution, DI/hosting, routing, and control-plane behavior.
- Runtime-owned public files no longer compile from the Core project.
- `Foundgine.Extensions` remains optional framework integration.
- `Foundgine.Providers` remains the concrete provider/infrastructure package.
- The AOT generator remains a separate build-time assembly/project but is packed inside `Foundgine.Providers` under `analyzers/dotnet/cs`; NuGet consumers do not install it separately.

## Application API

- `IFoundgineExecutor` is the minimal application execution interface.
- It exposes exactly two `ExecuteAsync` overloads (`SemanticRequest` and `ReadIntent`).
- `IFoundgine` extends that surface with capability discovery, dry-run, approval, and approved execution for advanced scenarios.
- Both interfaces resolve to the same `FoundgineEngine` singleton through DI.

## Static checks performed

- All `.csproj`, `.props`, and `.targets` files parse as XML.
- Every `ProjectReference` target exists.
- `Foundgine.sln` contains no duplicate project display names.
- No `Foundgine.Runtime.Execution` namespace references remain.
- Core source has no Runtime namespace dependency (friend-assembly declarations excluded).
- Four-package project dependency graph matches the intended v2 layering.
- `Foundgine.Providers` AOT analyzer packaging metadata is present.
- `IFoundgineExecutor` contains exactly two execution methods.

## Environment limitation

The working environment used for this pass does not provide the .NET SDK/`dotnet` CLI, so this report does **not** claim a successful `dotnet build`, `dotnet test`, or `dotnet pack`. Those commands should be run in the normal development/CI environment as the final compiler/runtime verification.
