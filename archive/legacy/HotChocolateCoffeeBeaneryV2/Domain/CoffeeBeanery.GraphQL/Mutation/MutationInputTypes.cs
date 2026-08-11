using Domain.Model;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;

namespace Api.Banking.Mutation;

public class WrapperInputType : InputObjectType<Wrapper>
{
    protected override void Configure(IInputObjectTypeDescriptor<Wrapper> inputObjectTypeDescriptor)
    {
    }
}

public class SortDefinition
{
    public string Name { get; set; } = default!;

    public bool IsLeaf { get; set; }

    public List<SortDefinition> Children { get; set; } = [];
}

public class SortDefinitionProvider : ISortDefinitionProvider
{
    public Task<SortDefinition> GetSortTreeAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new SortDefinition
            {
                Name = "Wrapper",
                Children =
                [
                    new()
                    {
                        Name = "customer",
                        Children =
                        [
                            new()
                            {
                                Name = "firstNaming",
                                IsLeaf = true
                            },
                            new()
                            {
                                Name = "contactPoint",
                                Children =
                                [
                                    new()
                                    {
                                        Name = "contactPointValue",
                                        IsLeaf = true
                                    },
                                    new()
                                    {
                                        Name = "contactPointType",
                                        IsLeaf = true
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });
    }
}

public enum SortDirection
{
    ASC,
    DESC
}

public interface ISortDefinitionProvider
{
    Task<SortDefinition> GetSortTreeAsync(
        CancellationToken cancellationToken);
}

public class DynamicSortModule : ITypeModule
{
    private readonly ISortDefinitionProvider _provider;

    public DynamicSortModule(ISortDefinitionProvider provider)
    {
        _provider = provider;
    }

    public event EventHandler<EventArgs>? TypesChanged;

    public async ValueTask<IReadOnlyCollection<ITypeSystemMember>> CreateTypesAsync(
        IDescriptorContext context,
        CancellationToken cancellationToken)
    {
        var root = await _provider.GetSortTreeAsync(cancellationToken);

        var allNodes = new List<SortDefinition>();
        Collect(root, allNodes);

        var types = new Dictionary<string, InputObjectTypeDefinition>();

        // STEP 1: create ALL type shells first
        foreach (var node in allNodes)
        {
            if (node.IsLeaf) continue;

            var type = new InputObjectTypeDefinition(node.Name + "SortInput");
            types[type.Name] = type;
        }

        // STEP 2: fill fields AFTER all types exist
        foreach (var node in allNodes)
        {
            if (node.IsLeaf) continue;

            var type = types[node.Name + "SortInput"];

            foreach (var child in node.Children)
            {
                if (child.IsLeaf)
                {
                    type.Fields.Add(new InputFieldDefinition
                    {
                        Name = child.Name,
                        Type = TypeReference.Create("SortDirection", TypeContext.Input)
                    });
                }
                else
                {
                    type.Fields.Add(new InputFieldDefinition
                    {
                        Name = child.Name,
                        Type = TypeReference.Create(child.Name + "SortInput", TypeContext.Input)
                    });
                }
            }
        }

        return types.Values
            .Select(InputObjectType.CreateUnsafe)
            .ToArray();
    }

    public class SortInput
    {
        public Model Model { get; set; }
        public string Path { get; set; } = default!;
        public SortDirection Direction { get; set; }
    }
    
    private void Collect(SortDefinition node, List<SortDefinition> list)
    {
        list.Add(node);

        foreach (var child in node.Children)
            Collect(child, list);
    }
}
