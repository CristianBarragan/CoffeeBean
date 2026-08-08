using HotChocolate.Execution.Processing;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

public static class FilterQueryExtension
{
    public static EntityFilterMetadata? CompileWhere(
        ISelection selection,
        ushort rootEntityId,
        FilterMetadataResolver resolver)
    {
        var where =
            FindWhereArgument(
                selection);


        if (where == null)
            return null;


        var expression =
            WhereCompiler.Compile(
                where);


        if (expression == null)
            return null;


        return resolver.Resolve(
            rootEntityId,
            expression);
    }



    private static ObjectValueNode? FindWhereArgument(
        ISelection selection)
    {
        foreach (var argument in selection.SyntaxNode.Arguments)
        {
            if (!string.Equals(
                    argument.Name.Value,
                    "where",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }


            return argument.Value as ObjectValueNode;
        }


        return null;
    }
}