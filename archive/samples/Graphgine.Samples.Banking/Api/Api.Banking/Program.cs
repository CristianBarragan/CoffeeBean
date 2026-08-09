using Amazon;
using Amazon.RDS.Util;
using Api.Banking.Mutation;
using Api.Banking.Query;
using Graphgine.Sql;
using Database.Entity.Banking;
using HotChocolate.AspNetCore;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.Banking;

public class Program
{
    public static void Main(string[] args)
    {
        var app = CreateHostBuilder(args);
        app.UseWebSockets();
        app.UseRouting();
        app.UseHttpsRedirection();
        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        app.MapGraphQL();
        app.MapNitroApp("/graphql-ui/").WithOptions(new GraphQLToolOptions()
            { ServeMode = GraphQLToolServeMode.Embedded });
        app.MapControllers();
        app.Run();
    }

    public static WebApplication CreateHostBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var services = builder.Services;

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables()
            .AddCommandLine(args).Build();

        var connectionString = configuration.GetConnectionString("BankingConnectionString");;
        var isRds = false;

        if (isRds)
        {
            services.AddNpgsqlDataSource(connectionString!, dataSourceBuilder =>
            {
                dataSourceBuilder.UsePeriodicPasswordProvider(async (settings, cancellationToken) =>
                    {
                        return await Task.Run(
                            () => RDSAuthTokenGenerator.GenerateAuthToken(RegionEndpoint.APSoutheast2, settings.Host,
                                settings.Port,
                                settings.Username), cancellationToken);
                    }, TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10));
            });
        }
        else
        {
            builder.Services.AddNpgsqlDataSource(connectionString!, ds =>
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.ClearProviders();
                    builder.SetMinimumLevel(LogLevel.None);
                });
                ds.UseLoggerFactory(loggerFactory);
            });
        }
        
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        
        builder.Services.AddScoped<Func<NpgsqlConnection>>(sp => () =>
        {
            var conn = new NpgsqlConnection(connectionString);
            conn.Open();
    
            using var initCmd = new NpgsqlCommand(
                @"LOAD 'age'; SET search_path = ag_catalog, ""$user"", public;", conn);
            initCmd.ExecuteNonQuery();
    
            return conn;
        });
        
        builder.Services.AddControllers().AddNewtonsoftJson();
        builder.Services.AddSingleton<
            ISortDefinitionProvider,
            SortDefinitionProvider>();
        
        services.AddDbContext<BankingEntityContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        // NOTE: there is deliberately no services.AddGraphgine(...) call here.
        // src/Graphgine.AspNetCore (which is where that extension method would
        // live) is an intentional placeholder -- see its README.md: the
        // maintainers chose not to invent an AddGraphgine()/MapGraphgine() API
        // surface without a second real consumer to validate the shape against.
        // Today, per that same README, wiring stays inline here instead.
        //
        // The pieces Graphgine actually needs at runtime -- IMetadataProvider,
        // IPlannerRegistry, IMutationMetadataProvider, IEnumConversionProvider --
        // are boundaries over source-generated code (Graphgine.SourceGenerators
        // emits GeneratedMetadataProvider / GeneratedPlannerRegistry /
        // GeneratedMutationMetadataProvider / GeneratedEnumConversionProvider
        // once mapping classes implementing Graphgine.Mapping.IMappingDefinition
        // exist in this project -- see Mapping/AccountMapping.cs for a first
        // one). Registering those bindings is intentionally left to whoever
        // finishes wiring the remaining mapping classes and can confirm the
        // generated types actually compile; see PORT-STATUS.md at the sample
        // root for the exact next steps and why they can't be done blind.
        //
        // services.AddSingleton<Foundgine.IMetadataProvider>(Foundgine.GeneratedMetadataProvider.Instance);
        // services.AddSingleton<Graphgine.Execution.IPlannerRegistry, Graphgine.Execution.GeneratedPlannerRegistry>();
        // services.AddSingleton<Graphgine.Execution.IMutationMetadataProvider, Graphgine.Execution.GeneratedMutationMetadataProvider>();
        // services.AddSingleton<Graphgine.Execution.IEnumConversionProvider, Graphgine.Execution.GeneratedEnumConversionProvider>();
        // services.AddScoped<Api.Banking.Service.IProcessService<Wrapper>, Api.Banking.Service.ProcessService<Wrapper>>();

        builder.Services.AddSingleton<
            DynamicSortModule>();
        builder.Services.AddGraphQLServer()
            .AddQueryType(d =>
            {
                d.Field("wrapper")
                    .ResolveWith<WrapperQueryResolver>(r => r.GetWrapper(default, default,
                        default, default));
            })
            .AddMutationType(d =>
            {
                d.Name("Mutation");

                d.Field("wrapper")
                    .Argument("wrapper", d => d.Type<WrapperInputType>())
                    .Argument("order", a =>
                        a.Type<AnyType>())
                    .ResolveWith<WrapperMutationResolver>(r => r.UpsertWrapper(default, default, default, default));
            })
            .SetPagingOptions(new PagingOptions() { DefaultPageSize = 10, IncludeTotalCount = true })
            .AddFiltering()
            .AddType<DynamicSortModule.SortInput>()
            .AddType<EnumType<SortDirection>>()
            .AddTypeModule<DynamicSortModule>()
            .InitializeOnStartup();

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        return builder.Build();
    }
}