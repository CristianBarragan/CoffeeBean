using System.Reflection;
using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Core.Warmup;
using CoffeeBeanery.GraphQL.Helper;
using Microsoft.Extensions.DependencyInjection;

public static class GraphWarmup
{
    private static bool _initialized;

    public static void Init<TSet>(
        this IServiceCollection services,
        Assembly assembly)
        where TSet : IMappingSet
    {
        if (_initialized) return;
        _initialized = true;

        var sets = assembly.GetTypes()
            .Where(t => typeof(TSet).IsAssignableFrom(t)
                        && !typeof(GraphModel).IsAssignableFrom(t)
                        && !t.IsInterface
                        && !t.IsAbstract)
            .Select(t => (TSet)Activator.CreateInstance(t)!)
            .ToList();
        
        // var enum1Values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToList();
        // var enum2Values = Enum.GetValues(typeof(T2Enum)).Cast<T2Enum>().ToList();

        // for (int i = 0; i < enum2Values.Count; i++)
        // {
        //     var set = sets.FirstOrDefault(a =>
        //         a.GetType().Name.Replace("MappingSet", "").Matches(enum2Values[i].ToString()) &&
        //         !a.GetType().Name.Replace("MappingSet", "").Matches(enum1Values[1].ToString()));
        //
        //     if (set == null)
        //         continue;
        //     
        //     set.Register();
        // }
        
        for (int i = 0; i < sets.Count; i++)
        {
            // var set = sets.FirstOrDefault(a =>
            //     a.GetType().Name.Replace("MappingSet", "").Matches(enum2Values[i].ToString()) &&
            //     !a.GetType().Name.Replace("MappingSet", "").Matches(enum1Values[0].ToString()));

            if (sets[i] == null)
                continue;
            
            sets[i].Register();
        }

        MappingWarmup.Warmup(MappingRegistry.Registry);

        services.AddSingleton<IMapper>(
            new Mapper(MappingRegistry.Registry));
    }
}