using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using CoffeeBeanery.GraphQL.Core.Mapping;

public interface IMapper
{
    object MapByAlias(Type entityType, object entity, string alias);
}

public class Mapper : IMapper
{
    private readonly Dictionary<string, NodeMap> _mappings;
    private readonly ConcurrentDictionary<string, PropertyInfo[]>         _propCache   = new();
    private readonly ConcurrentDictionary<string, Func<object, object>>   _getterCache = new();
    private readonly ConcurrentDictionary<string, Action<object, object>> _setterCache = new();

    public Mapper(Dictionary<string, NodeMap> mappings)
    {
        _mappings = mappings;
    }

    private PropertyInfo[] GetCachedProperties(Type type) =>
        _propCache.GetOrAdd(type.FullName!, _ =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance));

    private Func<object, object> GetCachedGetter(Type type, string propName)
    {
        var key = $"{type.FullName}.{propName}";
        return _getterCache.GetOrAdd(key, _ =>
        {
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetGetMethod() == null) return _ => null!;
            var target = Expression.Parameter(typeof(object), "target");
            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(
                    Expression.Call(Expression.Convert(target, type), prop.GetGetMethod()!),
                    typeof(object)),
                target).Compile();
        });
    }

    private Action<object, object> GetCachedSetter(Type type, string propName)
    {
        var key = $"{type.FullName}.{propName}";
        return _setterCache.GetOrAdd(key, _ =>
        {
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetSetMethod() == null) return (_, __) => { };
            var target = Expression.Parameter(typeof(object), "target");
            var value  = Expression.Parameter(typeof(object), "value");
            return Expression.Lambda<Action<object, object>>(
                Expression.Call(
                    Expression.Convert(target, type),
                    prop.GetSetMethod()!,
                    Expression.Convert(value, prop.PropertyType)),
                target, value).Compile();
        });
    }

    public object MapByAlias(Type entityType, object entity, string alias)
    {
        if (entity == null) return null!;
        if (!_mappings.TryGetValue(alias, out var nodeMap))
            throw new InvalidOperationException(
                $"No mapping registered for alias '{alias}'");

        var mapped = Activator.CreateInstance(nodeMap.ModelType)!;
        MapProperties(entity, mapped, nodeMap);
        return mapped;
    }

    private void MapProperties(object entity, object model, NodeMap nodeMap)
    {
        var entityType     = entity.GetType();
        var entityTypeName = entityType.Name;
        var modelType      = model.GetType();

        foreach (var fieldMap in nodeMap.FieldMaps)
        {
            if (!string.IsNullOrEmpty(fieldMap.DestinationEntity) &&
                !fieldMap.DestinationEntity.Equals(entityTypeName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var value = GetCachedGetter(entityType, fieldMap.DestinationName)(entity);
            if (value == null) continue;

            var targetProp = modelType.GetProperty(fieldMap.SourceName,
                BindingFlags.Public | BindingFlags.Instance);
            if (targetProp == null) continue;

            var actualType = Nullable.GetUnderlyingType(targetProp.PropertyType)
                             ?? targetProp.PropertyType;

            if (actualType.IsPrimitive ||
                actualType == typeof(string) ||
                actualType == typeof(Guid)   ||
                actualType == typeof(decimal))
            {
                SafeSet(targetProp, model, value);
            }
            else if (actualType.IsEnum)
            {
                if (fieldMap.ToEnum is { Count: > 0 })
                {
                    var valueStr = value.ToString()!;
                    if (fieldMap.ToEnum.TryGetValue(valueStr, out var enumInt))
                    {
                        targetProp.SetValue(model, Enum.ToObject(actualType, enumInt));
                        continue;
                    }
                    if (int.TryParse(valueStr, out var intVal))
                    {
                        var match = fieldMap.ToEnum.Values
                            .Cast<int?>()
                            .FirstOrDefault(v => v == intVal);
                        if (match.HasValue)
                        {
                            targetProp.SetValue(model, Enum.ToObject(actualType, match.Value));
                            continue;
                        }
                    }
                }

                var enumValue = value is string s
                    ? Enum.Parse(actualType, s, ignoreCase: true)
                    : Enum.ToObject(actualType, value);
                SafeSet(targetProp, model, enumValue);
            }
            else if (typeof(IEnumerable).IsAssignableFrom(actualType) && actualType.IsGenericType)
            {
                var list     = Activator.CreateInstance(targetProp.PropertyType) as IList;
                var itemType = targetProp.PropertyType.GetGenericArguments()[0];
                foreach (var item in (IEnumerable)value)
                    list!.Add(MapByAlias(itemType, item, nodeMap.Alias));
                SafeSet(targetProp, model, list);
            }
            else
            {
                var existing = targetProp.GetValue(model);
                var nested   = existing != null
                    ? MergeIntoExisting(existing, value, nodeMap)
                    : MapByAlias(actualType, value, nodeMap.Alias);

                SafeSet(targetProp, model, nested);
            }
        }
    }

    private void SafeSet(PropertyInfo prop, object instance, object value)
    {
        if (prop == null || value == null) return;

        var targetType = prop.PropertyType;

        if (targetType.IsGenericType &&
            typeof(IEnumerable).IsAssignableFrom(targetType))
        {
            var elementType = targetType.GetGenericArguments()[0];
            var existing    = prop.GetValue(instance);

            if (existing == null)
            {
                existing = Activator.CreateInstance(targetType);
                prop.SetValue(instance, existing);
            }

            var list = (IList)existing;

            if (value is IEnumerable enumerable && value is not string)
            {
                foreach (var v in enumerable)
                    if (v != null && elementType.IsAssignableFrom(v.GetType()))
                        list.Add(v);
            }
            else
            {
                if (elementType.IsAssignableFrom(value.GetType()))
                    list.Add(value);
            }
            return;
        }

        if (targetType.IsAssignableFrom(value.GetType()))
        {
            prop.SetValue(instance, value);
            return;
        }

        if (targetType == typeof(Guid) || targetType == typeof(Guid?))
        {
            var keyProp = value.GetType().GetProperty(prop.Name);
            prop.SetValue(instance, keyProp?.GetValue(value));
        }
    }

    private object MergeIntoExisting(object existing, object entity, NodeMap nodeMap)
    {
        var entityType = entity.GetType();
        var modelType  = existing.GetType();

        foreach (var fieldMap in nodeMap.FieldMaps)
        {
            if (!string.IsNullOrEmpty(fieldMap.DestinationEntity) &&
                !fieldMap.DestinationEntity.Equals(entityType.Name,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var value = GetCachedGetter(entityType, fieldMap.DestinationName)(entity);
            if (value == null) continue;

            modelType
                .GetProperty(fieldMap.SourceName, BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(existing, value);
        }

        return existing;
    }
}