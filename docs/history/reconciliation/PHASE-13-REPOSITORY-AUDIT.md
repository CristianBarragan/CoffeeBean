# Phase 13 — Repository Audit

## Purpose

Source-level reconciliation before adding further architecture. This audit identifies overlapping concepts and dependency-boundary risks. It does not claim a .NET compilation pass.

## Capability candidates

- `class SemanticAuthorizationCapabilityDiscovery` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `record SemanticAuthorizationCapability` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `record SemanticFieldAuthorizationCapability` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `record SemanticRelationshipAuthorizationCapability` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `class SemanticCapabilityContractDiscovery` — `foundgine_phase10_final/src/Foundgine.Semantics/Capabilities/SemanticCapabilityContract.cs`
- `record SemanticCapability` — `foundgine_phase10_final/src/Foundgine.Semantics/Capabilities/SemanticCapabilityContract.cs`
- `record SemanticCapabilityConstraint` — `foundgine_phase10_final/src/Foundgine.Semantics/Capabilities/SemanticCapabilityContract.cs`
- `record SemanticCapabilityContract` — `foundgine_phase10_final/src/Foundgine.Semantics/Capabilities/SemanticCapabilityContract.cs`
- `record SemanticCapabilityEffect` — `foundgine_phase10_final/src/Foundgine.Semantics/Capabilities/SemanticCapabilityContract.cs`
- `record SemanticCapabilityInput` — `foundgine_phase10_final/src/Foundgine.Semantics/Capabilities/SemanticCapabilityContract.cs`
- `class AuthorizationCapabilityTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationCapabilityTests.cs`
- `class SemanticAuthorizationCapabilityTests` — `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/SemanticAuthorizationTests.cs`
- `class SemanticCapabilityContractTests` — `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/SemanticCapabilityContractTests.cs`

## Plan candidates

- `class NoOpProviderPlanCache` — `foundgine_phase10_final/benchmarks/CoffeeBeanery.Performance/Foundgine.CoffeeBeanery.BenchmarkApi/Program.cs`
- `interface IProviderPlanCache` — `foundgine_phase10_final/src/Foundgine.Execution/IProviderPlanCache.cs`
- `interface IProviderPlanCompiler` — `foundgine_phase10_final/src/Foundgine.Execution/IProviderPlanCompiler.cs`
- `class MemoryProviderPlanCache` — `foundgine_phase10_final/src/Foundgine.Execution/MemoryProviderPlanCache.cs`
- `record ProviderMutationPlan` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationPlan.cs`
- `record ProviderMutationBatchPlan` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/ProviderMutationBatchPlan.cs`
- `record ProviderPlan` — `foundgine_phase10_final/src/Foundgine.Execution/ProviderPlan.cs`
- `class ProviderPlanCacheExtensions` — `foundgine_phase10_final/src/Foundgine.Execution/ProviderPlanCacheExtensions.cs`
- `record InMemoryPlan` — `foundgine_phase10_final/src/Foundgine.InMemory/InMemoryProvider.cs`
- `class FoundginePlanningMarker` — `foundgine_phase10_final/src/Foundgine.Planning/FoundginePlanningMarker.cs`
- `interface IPlanOptimizer` — `foundgine_phase10_final/src/Foundgine.Planning/IPlanOptimizer.cs`
- `interface IPlanner` — `foundgine_phase10_final/src/Foundgine.Planning/IPlanner.cs`
- `record MutationBatchPlan` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationBatchPlan.cs`
- `record MutationPlan` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationPlan.cs`
- `class MutationPlanner` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationPlanner.cs`
- `class PlanInspector` — `foundgine_phase10_final/src/Foundgine.Planning/PlanInspection.cs`
- `record PlanEffectSummary` — `foundgine_phase10_final/src/Foundgine.Planning/PlanInspection.cs`
- `record PlanInspection` — `foundgine_phase10_final/src/Foundgine.Planning/PlanInspection.cs`
- `record PlanInspectionNode` — `foundgine_phase10_final/src/Foundgine.Planning/PlanInspection.cs`
- `class Planner` — `foundgine_phase10_final/src/Foundgine.Planning/Planner.cs`
- `record SemanticPlan` — `foundgine_phase10_final/src/Foundgine.Planning/SemanticPlan.cs`
- `record SemanticPlanNode` — `foundgine_phase10_final/src/Foundgine.Planning/SemanticPlan.cs`
- `class SemanticPlanFingerprint` — `foundgine_phase10_final/src/Foundgine.Planning/SemanticPlanFingerprint.cs`
- `record SemanticPlanOptimizationResult` — `foundgine_phase10_final/src/Foundgine.Planning/SemanticPlanOptimizationResult.cs`
- `class SemanticPlanOptimizer` — `foundgine_phase10_final/src/Foundgine.Planning/SemanticPlanOptimizer.cs`
- `record SemanticMutationDependencyPlan` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlan.cs`
- `record SemanticMutationOperationPlan` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlan.cs`
- `record SemanticMutationPlan` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlan.cs`
- `class SemanticMutationPlanner` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlanner.cs`
- `record SqlBatchedMutationPlan` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlBatchedMutationPlan.cs`
- `record SqlMutationBatchPlan` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlMutationPlan.cs`
- `record SqlMutationPlan` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlMutationPlan.cs`
- `record SqlPaginationPlan` — `foundgine_phase10_final/src/Foundgine.Sql/SqlPlan.cs`
- `record SqlPlan` — `foundgine_phase10_final/src/Foundgine.Sql/SqlPlan.cs`
- `record MutationPlanApproval` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `record MutationPlanOperation` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `record PlanApproval` — `foundgine_phase10_final/src/Foundgine/PlanApproval.cs`
- `class TestProviderPlanCompiler` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationCapabilityTests.cs`
- `record TestPlan` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationCapabilityTests.cs`
- `record TestPlan` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationGoldenPathTests.cs`
- `class PlanStats` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/ComplexQueryPostgresE2ETests.cs`
- `record PlanNodeStats` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/ComplexQueryPostgresE2ETests.cs`
- `class ContextSafePlanCacheTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/ContextSafePlanCacheTests.cs`
- `record TestPlan` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/ContextSafePlanCacheTests.cs`
- `record TestProviderPlan` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/EvidenceTests.cs`
- `class PlanApprovalTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PlanApprovalTests.cs`
- `class TestProviderPlanCompiler` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PlanApprovalTests.cs`
- `record TestPlan` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PlanApprovalTests.cs`
- `class PlanCacheTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PlanCacheTests.cs`
- `record TestPlan` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PlanCacheTests.cs`
- `class PlanStats` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PostgresE2ETests.cs`
- `record PlanNodeStats` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PostgresE2ETests.cs`
- `class TestProviderPlanCompiler` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PublicApiTests.cs`
- `record TestPlan` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/PublicApiTests.cs`
- `class SemanticMutationPlannerTests` — `foundgine_phase10_final/tests/Foundgine.Intent.Json.Tests/SemanticMutationPlannerTests.cs`
- `class ConnectionPlanningTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/ConnectionPlanningTests.cs`
- `class MutationPlanningBoundaryTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/MutationPlanningBoundaryTests.cs`
- `class PlannerTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/PlannerTests.cs`
- `class PlanningDependencyBoundaryTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/PlanningDependencyBoundaryTests.cs`
- `class SemanticMutationPlannerTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/SemanticMutationPlannerTests.cs`
- `class SemanticPlanOptimizerTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/SemanticPlanOptimizerTests.cs`

## Receipt candidates

- `class ExecutionEvidenceFactory` — `foundgine_phase10_final/src/Foundgine.Execution/ExecutionEvidence.cs`
- `record ExecutionEvidence` — `foundgine_phase10_final/src/Foundgine.Execution/ExecutionEvidence.cs`
- `class ExecutionReceiptFactory` — `foundgine_phase10_final/src/Foundgine.Execution/ExecutionReceipt.cs`
- `record ExecutionReceipt` — `foundgine_phase10_final/src/Foundgine.Execution/ExecutionReceipt.cs`
- `record ResolutionEvidence` — `foundgine_phase10_final/src/Foundgine.Semantics/Resolution/ResolutionResult.cs`
- `record SemanticResultEvidence` — `foundgine_phase10_final/src/Foundgine.Semantics/Results/SemanticResult.cs`
- `class CapturingEvidenceCompiler` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/EvidenceTests.cs`
- `class CapturingEvidenceProvider` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/EvidenceTests.cs`
- `class EvidenceTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/EvidenceTests.cs`
- `class ExecutionReceiptUnificationTests` — `foundgine_phase10_final/tests/Foundgine.MCP.Tests/ExecutionReceiptUnificationTests.cs`
- `record ExecutionReceipt` — `src/Foundgine.Semantics/ExecutionReceipt.cs`

## Mutation candidates

- `class MutationMergeAndGraphIndexes` — `foundgine_phase10_final/benchmarks/CoffeeBeanery.Performance/CoffeeBeanery.Database/Migrations/20260812093000_MutationMergeAndGraphIndexes.cs`
- `class Mutation` — `foundgine_phase10_final/benchmarks/CoffeeBeanery.Performance/HotChocolate.CoffeeBeanery.BenchmarkApi/Program.cs`
- `interface IMutationSchema` — `foundgine_phase10_final/src/Foundgine.Abstractions/MutationSchema.cs`
- `record MutationEntitySchema` — `foundgine_phase10_final/src/Foundgine.Abstractions/MutationSchema.cs`
- `record MutationRelationshipSchema` — `foundgine_phase10_final/src/Foundgine.Abstractions/MutationSchema.cs`
- `class ExecutionMutationIRCompiler` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/ExecutionMutationIR.cs`
- `record ExecutionMutationIR` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/ExecutionMutationIR.cs`
- `interface IMutationBatchExecutionProvider` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/IMutationBatchExecutionProvider.cs`
- `interface IMutationExecutionProvider` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/IMutationExecutionProvider.cs`
- `record MutationBatchResult` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationBatchResult.cs`
- `class MutationDependencyGraph` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationDependencyGraph.cs`
- `class MutationDependencyLevels` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationDependencyLevels.cs`
- `record MutationExecutionLevels` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationExecutionLevels.cs`
- `class MutationMaterializedNode` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationMaterializedResult.cs`
- `record MutationMaterializedResult` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationMaterializedResult.cs`
- `record ProviderMutationPlan` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationPlan.cs`
- `record MutationResult` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationResult.cs`
- `class MutationResultMaterializer` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/MutationResultMaterializer.cs`
- `record PostgresMutationBatchBoundary` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/PostgresMutationBatchBoundary.cs`
- `record ProviderMutationBatchPlan` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/ProviderMutationBatchPlan.cs`
- `class SemanticMutationExecutionLowerer` — `foundgine_phase10_final/src/Foundgine.Execution/Mutation/SemanticMutationExecutionLowerer.cs`
- `class GraphQLMutationResultShaper` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/GraphQLMutationResultShaping.cs`
- `record GraphQLMutationAdaptation` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/GraphQLMutationResultShaping.cs`
- `record GraphQLMutationBatchItem` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/GraphQLMutationResultShaping.cs`
- `record GraphQLMutationResultField` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/GraphQLMutationResultShaping.cs`
- `record GraphQLMutationResultRelationship` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/GraphQLMutationResultShaping.cs`
- `record GraphQLMutationResultShape` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/GraphQLMutationResultShaping.cs`
- `class HotChocolateMutationAdapter` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/HotChocolateMutationAdapter.cs`
- `record MutationBuildResult` — `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/HotChocolateMutationAdapter.cs`
- `class FoundgineMcpMutationTools` — `foundgine_phase10_final/src/Foundgine.MCP/FoundgineMcpMutationTools.cs`
- `class MutationJsonAdapter` — `foundgine_phase10_final/src/Foundgine.MCP/FoundgineMcpMutationTools.cs`
- `interface IMutationIntent` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/IMutationIntent.cs`
- `class MutationAuthorizer` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationAuthorizer.cs`
- `record MutationBatchIntent` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationBatchIntent.cs`
- `record MutationBatchPlan` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationBatchPlan.cs`
- `record MutationDependency` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationDependency.cs`
- `record MutationFieldValue` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationFieldValue.cs`
- `record MutationIntent` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationIntent.cs`
- `record MutationOperation` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationOperation.cs`
- `record MutationPlan` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationPlan.cs`
- `class MutationPlanner` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationPlanner.cs`
- `record MutationValueReference` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationValueReference.cs`
- `record NestedMutationChild` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/NestedMutationIntent.cs`
- `record NestedMutationIntent` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/NestedMutationIntent.cs`
- `class SemanticMutationBuilder` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationBuilder.cs`
- `record SemanticMutationDependency` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationDependency.cs`
- `record SemanticMutationEffect` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationEffect.cs`
- `record SemanticMutationField` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationField.cs`
- `record SemanticMutationOperation` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationOperation.cs`
- `record SemanticMutationOperationGraph` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationOperationGraph.cs`
- `record SemanticMutationDependencyPlan` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlan.cs`
- `record SemanticMutationOperationPlan` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlan.cs`
- `record SemanticMutationPlan` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlan.cs`
- `class SemanticMutationPlanner` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationPlanner.cs`
- `record SemanticMutationValueReference` — `foundgine_phase10_final/src/Foundgine.Semantics/Mutation/SemanticMutationValueReference.cs`
- `class PostgresBatchedMutationExecutionProvider` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/Postgres/PostgresBatchedMutationExecutionProvider.cs`
- `class PostgresBatchedMutationCompiler` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/PostgresBatchedMutationCompiler.cs`
- `record SqlBatchedMutationPlan` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlBatchedMutationPlan.cs`
- `class SqlMutationCompiler` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlMutationCompiler.cs`
- `class SqlMutationExecutionProvider` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlMutationExecutionProvider.cs`
- `record MutationReturnBinding` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlMutationPlan.cs`
- `record SqlMutationBatchPlan` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlMutationPlan.cs`
- `record SqlMutationPlan` — `foundgine_phase10_final/src/Foundgine.Sql/Mutation/SqlMutationPlan.cs`
- `class FoundgineMutationEngine` — `foundgine_phase10_final/src/Foundgine/FoundgineMutationEngine.cs`
- `interface IFoundgineMutations` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `record MutationDryRunResult` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `record MutationExecutionResult` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `record MutationPlanApproval` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `record MutationPlanOperation` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `record SemanticMutationRequest` — `foundgine_phase10_final/src/Foundgine/IFoundgineMutations.cs`
- `class ComplexSemanticMutationE2ETests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/ComplexSemanticMutationE2ETests.cs`
- `class ExecutionMutationIRTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/ExecutionMutationIRTests.cs`
- `class MutationDependencyGraphTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/MutationDependencyTests.cs`
- `class MutationDependencyTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/MutationDependencyTests.cs`
- `class MutationQueryMutationQueryIntegrationE2ETests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/MutationQueryMutationQueryIntegrationE2ETests.cs`
- `class MutationResultMaterializationTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/MutationResultMaterializationTests.cs`
- `class NestedMutationTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/NestedMutationTests.cs`
- `class GraphQLMutationAcceptanceTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/GraphQLMutationAcceptanceTests.cs`
- `class MutationAdapterTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationAdapterTests.cs`
- `class MutationAliasTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationAliasTests.cs`
- `class MutationFragmentTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationFragmentTests.cs`
- `class MutationOperationSelectionTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationOperationSelectionTests.cs`
- `class MutationTypeSemanticsTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationTypeSemanticsTests.cs`
- `class MutationVariableCoercionTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationVariableCoercionTests.cs`
- `class MutationVariableTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationVariableTests.cs`
- `class MutationVariablesTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/MutationVariablesTests.cs`
- `class NestedMutationResultShapingTests` — `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/NestedMutationResultShapingTests.cs`
- `class SemanticMutationPlannerTests` — `foundgine_phase10_final/tests/Foundgine.Intent.Json.Tests/SemanticMutationPlannerTests.cs`
- `class McpMutationBoundaryTests` — `foundgine_phase10_final/tests/Foundgine.MCP.Tests/McpMutationBoundaryTests.cs`
- `class MutationAuthorizationTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/MutationPlanningBoundaryTests.cs`
- `class MutationPlanningBoundaryTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/MutationPlanningBoundaryTests.cs`
- `class TestMutationSchema` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/MutationPlanningBoundaryTests.cs`
- `class SemanticMutationPlannerTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/SemanticMutationPlannerTests.cs`
- `class TestMutationSchema` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/SemanticMutationPlannerTests.cs`
- `class SemanticMutationIrTests` — `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/SemanticMutationIrTests.cs`

## Version candidates

- `record SemanticVersionSet` — `foundgine_phase10_final/src/Foundgine.Semantics/SemanticVersion.cs`
- `class SemanticVersioningTests` — `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/SemanticCapabilityContractTests.cs`

## Authorization candidates

- `record AuthorizationDecision` — `foundgine_phase10_final/src/Foundgine.Abstractions/AuthorizationDecision.cs`
- `struct AuthorizationId` — `foundgine_phase10_final/src/Foundgine.Abstractions/AuthorizationId.cs`
- `record AuthorizationPredicate` — `foundgine_phase10_final/src/Foundgine.Abstractions/AuthorizationPredicate.cs`
- `class FoundgineAuthorizationAttribute` — `foundgine_phase10_final/src/Foundgine.Aot/FoundgineAttributes.cs`
- `record AuthorizationMetadata` — `foundgine_phase10_final/src/Foundgine.Metadata/AuthorizationMetadata.cs`
- `class MutationAuthorizer` — `foundgine_phase10_final/src/Foundgine.Planning/Mutation/MutationAuthorizer.cs`
- `class AllowAllSemanticAuthorizationPolicy` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/AllowAllSemanticAuthorizationPolicy.cs`
- `interface ISemanticAuthorizationPolicy` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/ISemanticAuthorizationPolicy.cs`
- `class SemanticAuthorizationCapabilityDiscovery` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `record SemanticAuthorizationCapabilities` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `record SemanticAuthorizationCapability` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `record SemanticFieldAuthorizationCapability` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `record SemanticRelationshipAuthorizationCapability` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationCapability.cs`
- `class SemanticAuthorizationException` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizationException.cs`
- `class SemanticAuthorizer` — `foundgine_phase10_final/src/Foundgine.Semantics/Authorization/SemanticAuthorizer.cs`
- `class SqlAuthorizationWriter` — `foundgine_phase10_final/src/Foundgine.Sql/Query/SqlAuthorizationWriter.cs`
- `record SqlAuthorizationPredicate` — `foundgine_phase10_final/src/Foundgine.Sql/SqlPlan.cs`
- `class ProductAuthorization` — `foundgine_phase10_final/tests/Foundgine.Aot.Tests/GeneratedMetadataTests.cs`
- `class AuthorizationCapabilityTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationCapabilityTests.cs`
- `class Authorization` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationFixture.cs`
- `class AuthorizationGoldenPathTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationGoldenPathTests.cs`
- `class AuthorizationSqlExecutionTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/AuthorizationSqlExecutionTests.cs`
- `class NestedAuthorizationExecutionTests` — `foundgine_phase10_final/tests/Foundgine.E2E.Tests/NestedAuthorizationExecutionTests.cs`
- `class AuthorizationInvariantTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/AuthorizationInvariantTests.cs`
- `class MutationAuthorizationTests` — `foundgine_phase10_final/tests/Foundgine.Planning.Tests/MutationPlanningBoundaryTests.cs`
- `class SemanticAuthorizationCapabilityTests` — `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/SemanticAuthorizationTests.cs`
- `class SemanticAuthorizationTests` — `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/SemanticAuthorizationTests.cs`

## Core dependency boundary scan

Potential forbidden references detected:

- `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/SemanticCapabilityContractTests.cs` references `Npgsql`

## Audit interpretation

This report is intentionally a candidate inventory. A symbol is not declared duplicate merely because it contains a similar word. The next real build environment should resolve actual type identity, project references, accessibility, and compilation conflicts.
