using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

internal sealed class MutationPlanInfo
{
    public List<MutationRowInfo> Rows { get; } = new();

    public List<MutationGraphInfo> Graphs { get; } = new();

    public List<MutationCteInfo> Ctes { get; } = new();
}

internal sealed class MutationRowInfo
{
    public INamedTypeSymbol Entity = null!;

    public string Alias = "";

    public bool IsPrimary;

    public List<MutationColumnInfo> Columns { get; } = new();
}

internal sealed class MutationColumnInfo
{
    public FieldInfo Field = null!;

    public string Column = "";

    public string ModelField = "";
}

internal sealed class MutationGraphInfo
{
    public GraphInfo Graph = null!;

    public MutationRowInfo From = null!;

    public MutationRowInfo To = null!;
}

public sealed class MutationCteInfo
{
    public EntityDefinitionInfo Entity = null!;

    public List<FieldInfo> KeyFields { get; } = new();

    public List<MutationCteInfo> Children { get; } = new();
}