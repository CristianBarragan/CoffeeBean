using CoffeeBeanery.GraphQL.Core.Mapping;

namespace CoffeeBeanery.GraphQL.Core.Helper
{
    using System;
    using System.Linq.Expressions;

    public static class BulkMapper
    {
        public static void Compile(NodeMap map)
        {
            map.CreateMapper = CompileCreate(map);
            map.UpdateMapper = CompileUpdate(map);
        }

        private static Func<object, object> CompileCreate(NodeMap map)
        {
            var srcParam = Expression.Parameter(typeof(object), "src");

            var typedSrc = Expression.Convert(srcParam, map.ModelType);

            var destVar = Expression.Variable(map.EntityType, "dest");

            var expressions = new List<Expression>();

            expressions.Add(
                Expression.Assign(destVar, Expression.New(map.EntityType))
            );

            foreach (var field in map.FieldMaps.Where(f => f.SourceName != "Id"))
            {
                if (!map.ModelProperties.ContainsKey(field.SourceName) ||
                    !map.EntityProperties.ContainsKey(field.DestinationName))
                {
                    continue;
                }
                
                var srcProp = map.ModelProperties[field.SourceName];
                var destProp = map.EntityProperties[field.DestinationName];

                var srcAccess = Expression.Property(typedSrc, srcProp);
                var destAccess = Expression.Property(destVar, destProp);
                var converted = ConvertValue(srcAccess, destProp.PropertyType);

                if (converted != null)
                {
                    var assign = Expression.Assign(destAccess, converted);

                    expressions.Add(assign);    
                }
            }

            expressions.Add(destVar);

            var body = Expression.Block(
                new[] { destVar },
                expressions
            );

            var lambda = Expression.Lambda<Func<object, object>>(
                Expression.Convert(body, typeof(object)),
                srcParam
            );

            return lambda.Compile();
        }
        
        private static Expression ConvertValue(Expression value, Type destinationType)
        {
            var sourceType = value.Type;
            
            if (sourceType == destinationType)
                return value;

            if (Nullable.GetUnderlyingType(sourceType) == destinationType)
            {
                return Expression.Property(value, "Value");
            }

            if (Nullable.GetUnderlyingType(destinationType) == sourceType)
            {
                return Expression.Convert(value, destinationType);
            }
            
            return default;
        }

        private static Action<object, object> CompileUpdate(NodeMap map)
        {
            var srcParam = Expression.Parameter(typeof(object), "src");
            var destParam = Expression.Parameter(typeof(object), "dest");

            var typedSrc = Expression.Convert(srcParam, map.ModelType);
            var typedDest = Expression.Convert(destParam, map.EntityType);

            var expressions = new List<Expression>();

            foreach (var field in map.FieldMaps.Where(f => f.SourceName != "Id"))
            {
                if (!map.ModelProperties.ContainsKey(field.SourceName) ||
                    !map.EntityProperties.ContainsKey(field.DestinationName))
                {
                    continue;
                }
                
                var srcProp = map.ModelProperties[field.SourceName];
                var destProp = map.EntityProperties[field.DestinationName];

                var srcAccess = Expression.Property(typedSrc, srcProp);
                var destAccess = Expression.Property(typedDest, destProp);

                var converted = ConvertValue(srcAccess, destProp.PropertyType);

                if (converted != null)
                {
                    var assign = Expression.Assign(destAccess, converted);

                    expressions.Add(assign);    
                }
            }

            var body = Expression.Block(expressions);

            var lambda = Expression.Lambda<Action<object, object>>(
                body,
                srcParam,
                destParam
            );

            return lambda.Compile();
        }
    }
}
//     
//     
//     public static class BulkMapper
//     {
//         public static Action<TSrc, TDst> Compile<TSrc, TDst>(
//             PropertyMapping<TSrc, TDst>[] maps)
//         {
//             var compiledMaps = maps.Select(m => new
//             {
//                 Getter = m.SourceExpression.Compile(),
//                 Property = GetProperty<TSrc,TDst>(m.DestinationExpression),
//                 FromEnum = m.FromEnum,
//                 ToEnum = m.ToEnum
//             }).ToArray();
//
//             return (src, dst) =>
//             {
//                 foreach (var m in compiledMaps)
//                 {
//                     object value = m.Getter(src);
//
//                     if (value != null && m.FromEnum != null)
//                     {
//                         if (m.FromEnum.TryGetValue(value.ToString(), out var mapped))
//                             value = mapped;
//                     }
//
//                     m.Property.SetValue(dst, value);
//                 }
//             };
//         }
//
//         private static PropertyInfo GetProperty<TSrc, TDst>(
//             Expression<Func<TDst, object>> expression)
//         {
//             var body = expression.Body;
//
//             if (body is UnaryExpression ue)
//                 body = ue.Operand;
//
//             return (PropertyInfo)((MemberExpression)body).Member;
//         }
//     }
// }