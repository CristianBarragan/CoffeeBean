using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Graphgine.Mapping;

public sealed class EfEntityMetadata<TContext>
    where TContext : DbContext
{
    private readonly IModel _model;

    public EfEntityMetadata(TContext context)
    {
        _model = context.Model;
    }

    public IEntityType RequireEntityType(
        Type entityType,
        string mappingContext)
    {
        var efType = _model.FindEntityType(entityType);

        if (efType == null)
        {
            throw new InvalidOperationException(
                $"[NodeBuilder] Entity type '{entityType.FullName}' " +
                $"(referenced by mapping '{mappingContext}') was not found in the EF model.");
        }

        return efType;
    }

    public List<NavigationDefinition> GetNavigations(IEntityType efEntityType)
    {
        var result = new List<NavigationDefinition>();

        foreach (var nav in efEntityType.GetNavigations())
        {
            var fk = nav.ForeignKey;

            // Orient FromColumn/ToColumn based on which side THIS entity is on.
            var (fromEntity, fromColumn, toEntity, toColumn) = nav.IsOnDependent
                ? (efEntityType.ClrType, fk.Properties[0].Name, nav.TargetEntityType.ClrType, fk.PrincipalKey.Properties[0].Name)
                : (efEntityType.ClrType, fk.PrincipalKey.Properties[0].Name, nav.TargetEntityType.ClrType, fk.Properties[0].Name);

            result.Add(new NavigationDefinition
            {
                NavigationName = nav.Name,
                TargetModel = nav.TargetEntityType.ClrType,
                IsCollection = nav.IsCollection,
                Paths =
                [
                    new JoinPathDefinition
                    {
                        TargetEntity = nav.TargetEntityType.ClrType,
                        Hops =
                        [
                            new JoinHopDefinition
                            {
                                FromEntity = fromEntity,
                                FromColumn = fromColumn,
                                ToEntity = toEntity,
                                ToColumn = toColumn
                            }
                        ]
                    }
                ]
            });
        }

        return result;
    }

    public sealed class NavigationInfo
    {
        public string NavigationName { get; init; } = "";
        
        public INamedTypeSymbol? TargetModel { get; init; } 
        public string ForeignKeyProperty { get; init; } = "";
        public string PrincipalKeyProperty { get; init; } = "";
        public Type RelatedEntityType { get; init; } = typeof(object);
        public bool IsCollection { get; init; }

        // True when the entity this NavigationInfo was generated for is the dependent side
        // of the relationship (i.e. THIS entity holds the FK column, ForeignKeyProperty).
        // False (principal side) means the RELATED entity holds the FK column instead.
        public bool IsOnDependent { get; init; }
    }
}