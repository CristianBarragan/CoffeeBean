using System;
using System.Collections.Immutable;
using System.Linq;

namespace Graphgine.Execution.Filtering;

public sealed class FilterMetadataResolver
{
    private readonly ImmutableArray<RuntimeEntityMetadata> _entities;


    public FilterMetadataResolver(
        ImmutableArray<RuntimeEntityMetadata> entities)
    {
        _entities = entities;
    }


    public EntityFilterMetadata Resolve(
        ushort entityId,
        FilterExpression expression)
    {
        var entity =
            _entities.First(x =>
                x.EntityId == entityId);


        return ResolveExpression(
            entity,
            expression);
    }


    private EntityFilterMetadata ResolveExpression(
        RuntimeEntityMetadata entity,
        FilterExpression expression)
    {
        return expression switch
        {
            BinaryFilterExpression binary =>
                ResolveBinary(
                    entity,
                    binary),


            NavigationFilterExpression navigation =>
                ResolveNavigation(
                    entity,
                    navigation),


            CollectionFilterExpression collection =>
                ResolveCollection(
                    entity,
                    collection),


            AndFilterExpression and =>
                new EntityFilterMetadata.And(
                    and.Expressions
                        .Select(x =>
                            ResolveExpression(
                                entity,
                                x))
                        .ToList()),


            OrFilterExpression or =>
                new EntityFilterMetadata.Or(
                    or.Expressions
                        .Select(x =>
                            ResolveExpression(
                                entity,
                                x))
                        .ToList()),


            _ =>
                throw new NotSupportedException(
                    expression.GetType().Name)
        };
    }



    private EntityFilterMetadata ResolveBinary(
        RuntimeEntityMetadata entity,
        BinaryFilterExpression binary)
    {
        var field =
            entity.Fields.Values
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Name,
                        binary.FieldName,
                        StringComparison.OrdinalIgnoreCase));


        if (field == null)
        {
            throw new InvalidOperationException(
                $"Unknown filter field '{binary.FieldName}' on {entity.Name}");
        }


        return new EntityFilterMetadata.Field(
            entity,
            field,
            binary.Operator,
            binary.Value);
    }



    private EntityFilterMetadata ResolveNavigation(
        RuntimeEntityMetadata entity,
        NavigationFilterExpression navigation)
    {
        var nav =
            entity.Navigations
                .FirstOrDefault(x =>
                    string.Equals(
                        x.NavigationName,
                        navigation.NavigationName,
                        StringComparison.OrdinalIgnoreCase));


        if (nav == null)
        {
            throw new InvalidOperationException(
                $"Unknown navigation '{navigation.NavigationName}' on {entity.Name}");
        }


        var target =
            _entities.First(x =>
                x.EntityId == nav.TargetEntityId);


        return new EntityFilterMetadata.Navigation(
            nav,
            ResolveExpression(
                target,
                navigation.Inner));
    }



    private EntityFilterMetadata ResolveCollection(
        RuntimeEntityMetadata entity,
        CollectionFilterExpression collection)
    {
        return new EntityFilterMetadata.Collection(
            collection.Operator,
            ResolveExpression(
                entity,
                collection.Inner));
    }
}