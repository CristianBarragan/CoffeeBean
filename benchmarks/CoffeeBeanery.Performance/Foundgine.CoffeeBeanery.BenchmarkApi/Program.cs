using Foundgine.CoffeeBeanery.BenchmarkApi;
using Foundgine.Execution;
using Foundgine.Execution.Mutation;
using Foundgine.GraphQL.HotChocolate;
using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.Planning.Mutation;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Resolution;
using Foundgine.Sql;
using Foundgine.Sql.Mutation;
using Npgsql;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

var builder = WebApplication.CreateBuilder(args);
var connectionString =
    builder.Configuration.GetConnectionString("BankingConnectionString")
    ?? throw new InvalidOperationException(
        "Connection string 'BankingConnectionString' is not configured.");

var model = CoffeeBeanerySemanticModel.Build();
var metadata = CoffeeBeaneryMetadata.Build();
var policy = new AllowAllSemanticAuthorizationPolicy();
var resolver = new SemanticRequestResolver(model);
var authorizer = new SemanticAuthorizer(policy);
var planner = new Planner();
var compiler = new SqlCompiler(metadata);
var warmQueryCache = new MemoryProviderPlanCache();

var mutationAdapter = new HotChocolateMutationAdapter(model, metadata);
var mutationPlanner = new MutationPlanner(metadata);
var mutationAuthorizer = new MutationAuthorizer(metadata, policy);
var mutationCompiler = new SqlMutationCompiler(metadata);
var mutationMaterializer = new MutationResultMaterializer(model);

var app = builder.Build();

app.MapPost("/graphql/{mode}", async (
    string mode,
    GraphQLRequest request,
    CancellationToken cancellationToken) =>
{
    if (!string.Equals(mode, "cold", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(mode, "warm", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Mode must be 'cold' or 'warm'." });
    }

    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await SetSearchPathAsync(connection, cancellationToken);

        if (request.Query.TrimStart().StartsWith("mutation", StringComparison.OrdinalIgnoreCase))
        {
            var adaptation = mutationAdapter.AdaptResultShape(
                request.Query,
                request.Variables,
                request.OperationName);

            var plan = mutationPlanner.Plan(adaptation.Intent);
            mutationAuthorizer.Authorize(plan);
            var providerPlan = mutationCompiler.Compile(plan);

            var result = new SqlMutationExecutionProvider(connection)
                .ExecuteBatch(providerPlan, new ExecutionContext());

            var materialized = mutationMaterializer.Materialize(adaptation.Intent, result);
            var root = materialized.Roots.FirstOrDefault();
            var shaped = root is null
                ? null
                : GraphQLMutationResultShaper.ShapeRoot(materialized, adaptation.ResultShape);

            var operationField = GetMutationFieldName(request.OperationName);

            return Results.Json(new Dictionary<string, object?>
            {
                ["data"] = new Dictionary<string, object?>
                {
                    [operationField] = shaped
                }
            });
        }

        var queryAdaptation = new HotChocolateSemanticAdapter(model)
            .AdaptResultShape(request.Query, request.Variables, request.OperationName);
        var resolved = resolver.Resolve(queryAdaptation.Request);
        var authorized = authorizer.Authorize(resolved);
        var planQuery = planner.Plan(authorized);

        var cache = string.Equals(mode, "warm", StringComparison.OrdinalIgnoreCase)
            ? (IProviderPlanCache)warmQueryCache
            : NoOpProviderPlanCache.Instance;

        var cacheKey = ExecutionPlanFingerprint.CreateShapeKey(planQuery);
        var providerPlanQuery = cache.GetOrAdd(
            cacheKey,
            () => compiler.Compile(planQuery));

        var executionContext = BuildExecutionContext(planQuery);
        var execution = await new SqlExecutionProvider(connection)
            .ExecuteAsync(providerPlanQuery, executionContext, cancellationToken);

        return Results.Json(GraphQLResultShaper.Shape(
            queryAdaptation,
            model,
            execution,
            planQuery));
    }
    catch (Exception ex)
	{
		app.Logger.LogError(
			ex,
			"GraphQL request failed. Mode={Mode}, OperationName={OperationName}",
			mode,
			request.OperationName);

		return Results.Json(
			new
			{
				errors = new[]
				{
					new
					{
						message = ex.Message
					}
				}
			},
			statusCode: 400);
	}
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await SetSearchPathAsync(connection, cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database is not ready.",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.Run();

static ExecutionContext BuildExecutionContext(Foundgine.Planning.ExecutionPlan plan)
{
    var options = plan.Root.QueryOptions;
    if (options?.Limit is null && options?.Offset is null)
        return new ExecutionContext();

    var values = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (options.Limit is { } limit)
        values[Foundgine.Execution.ExecutionContextKeys.PaginationLimit] =
            limit + (options.After is not null ? 1 : 0);
    if (options.Offset is { } offset)
        values[Foundgine.Execution.ExecutionContextKeys.PaginationOffset] = offset;
    values[Foundgine.Execution.ExecutionContextKeys.PaginationHasCursor] = options.After is not null;

    return new ExecutionContext(values);
}

static string GetMutationFieldName(string? operationName)
{
    if (string.IsNullOrWhiteSpace(operationName))
        throw new InvalidOperationException("Mutation operationName is required by the benchmark API.");

    return char.ToLowerInvariant(operationName[0]) + operationName[1..];
}

static async Task SetSearchPathAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
{
    await using var command = connection.CreateCommand();
    command.CommandText = "SET search_path TO \"Banking\", \"Lending\", \"Accounting\";";
    await command.ExecuteNonQueryAsync(cancellationToken);
}

internal sealed record GraphQLRequest(
    string Query,
    Dictionary<string, object?>? Variables = null,
    string? OperationName = null);

internal sealed class NoOpProviderPlanCache : IProviderPlanCache
{
    internal static readonly NoOpProviderPlanCache Instance = new();

    public bool TryGet(string key, out ProviderPlan plan)
    {
        plan = null!;
        return false;
    }

    public void Set(string key, ProviderPlan plan) { }
}
