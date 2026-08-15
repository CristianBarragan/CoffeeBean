using System.ComponentModel;
using System.Text.Json;
using Foundgine.Abstractions;
using Foundgine.Semantics.Mutation;
using Foundgine.Semantics.Query;
using ModelContextProtocol.Server;

namespace Foundgine.MCP;

[McpServerToolType]
public sealed class FoundgineMcpMutationTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IFoundgineMutations? _mutations;

    public FoundgineMcpMutationTools(IFoundgineMutations? mutations = null) =>
        _mutations = mutations;

    [McpServerTool(Name = "foundgine_mutation_dry_run")]
    [Description("Validate and authorize a Foundgine mutation without executing it. Returns the exact semantic plan fingerprint and declared effects.")]
    public string DryRun(string mutationJson)
    {
        var mutations = RequireMutations();
        var request = MutationJsonAdapter.Parse(mutationJson);
        return JsonSerializer.Serialize(mutations.DryRun(request), JsonOptions);
    }

    [McpServerTool(Name = "foundgine_mutation_approve")]
    [Description("Create an approval bound to the exact authorized Foundgine mutation plan. Approval is not authorization and execution re-checks the plan.")]
    public string Approve(string mutationJson, string approvedBy)
    {
        var mutations = RequireMutations();
        var request = MutationJsonAdapter.Parse(mutationJson);
        return JsonSerializer.Serialize(mutations.Approve(request, approvedBy), JsonOptions);
    }

    [McpServerTool(Name = "foundgine_mutation_execute_approved")]
    [Description("Execute only an approved Foundgine mutation after re-authorization and exact plan fingerprint verification. Pass the approval JSON returned by foundgine_mutation_approve.")]
    public async Task<string> ExecuteApprovedAsync(string approvalJson, CancellationToken cancellationToken = default)
    {
        var approval = JsonSerializer.Deserialize<MutationPlanApproval>(approvalJson, JsonOptions)
            ?? throw new ArgumentException("Approval JSON is invalid.", nameof(approvalJson));
        var result = await RequireMutations().ExecuteApprovedAsync(approval, cancellationToken: cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
    private IFoundgineMutations RequireMutations() => _mutations ??
        throw new InvalidOperationException("Foundgine mutation execution is not configured. Configure FoundgineOptions.MutationSchema and MutationProvider.");
}

internal static class MutationJsonAdapter
{
    public static SemanticMutationRequest Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Mutation JSON is required.", nameof(json));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array || operations.GetArrayLength() == 0)
            throw new ArgumentException("'operations' must contain at least one mutation operation.", nameof(json));

        var list = new List<SemanticMutationOperation>();
        foreach (var op in operations.EnumerateArray()) list.Add(ParseOperation(op, list.Count));
        return new SemanticMutationRequest(new SemanticMutationOperationGraph(list));
    }

    private static SemanticMutationOperation ParseOperation(JsonElement op, int index)
    {
        var entity = new EntityId(GetUShort(op, "entity"));
        var kind = Enum.Parse<SemanticMutationKind>(GetString(op, "kind"), true);
        var fields = new List<SemanticMutationField>();
        if (op.TryGetProperty("fields", out var fieldObject) && fieldObject.ValueKind == JsonValueKind.Object)
            foreach (var p in fieldObject.EnumerateObject()) fields.Add(new SemanticMutationField(new FieldId(ushort.Parse(p.Name)), JsonElementToValue(p.Value)));

        var returns = ParseIds(op, "returnFields");
        var conflicts = ParseIds(op, "conflictFields");
        SemanticFilterExpression? filter = op.TryGetProperty("filter", out var filterJson) ? ParseFilter(filterJson) : null;

        return new SemanticMutationOperation(
            entity, kind, fields, filter, conflicts, returns,
            kind switch
            {
                SemanticMutationKind.Create => SemanticMutationBuilder.Create(entity, fields, returns).Effects,
                SemanticMutationKind.Update => SemanticMutationBuilder.Update(entity, fields, filter, returns).Effects,
                SemanticMutationKind.Delete => SemanticMutationBuilder.Delete(entity, filter ?? throw new ArgumentException("Delete requires a filter."), returns).Effects,
                SemanticMutationKind.Upsert => SemanticMutationBuilder.Upsert(entity, fields, conflicts, returns).Effects,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            },
            Array.Empty<SemanticMutationDependency>());
    }

    private static SemanticFilterExpression ParseFilter(JsonElement json)
    {
        if (json.TryGetProperty("and", out var and)) return new SemanticAndFilter(and.EnumerateArray().Select(ParseFilter).ToArray());
        if (json.TryGetProperty("or", out var or)) return new SemanticOrFilter(or.EnumerateArray().Select(ParseFilter).ToArray());
        return new SemanticFieldFilter(
            new FieldId(GetUShort(json, "field")),
            Enum.Parse<SemanticFilterOperator>(GetString(json, "operator"), true),
            json.TryGetProperty("value", out var value) ? JsonElementToValue(value) : null);
    }

    private static IReadOnlyList<FieldId> ParseIds(JsonElement op, string property) =>
        op.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(x => new FieldId(x.GetUInt16())).ToArray()
            : Array.Empty<FieldId>();

    private static ushort GetUShort(JsonElement obj, string property) => obj.GetProperty(property).GetUInt16();
    private static string GetString(JsonElement obj, string property) => obj.GetProperty(property).GetString() ?? throw new ArgumentException($"'{property}' is required.");

    private static object? JsonElementToValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var l) => l,
        JsonValueKind.Number when value.TryGetDecimal(out var d) => d,
        JsonValueKind.String => value.GetString(),
        _ => value.Clone()
    };
}
