using Amazon;
using Amazon.RDS.Util;
using Api.Banking.Mutation;
using Api.Banking.Query;
using Domain.Shared.Extension;
using HotChocolate.AspNetCore;
using HotChocolate.Types.Pagination;
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

        var connectionString = configuration.GetConnectionString("BankingConnectionString");

        services.AddCoffeeBeanery(connectionString);
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

        builder.Services.AddSingleton<
            DynamicSortModule>();
        builder.Services.AddGraphQLServer()
            .AddQueryType(d =>
            {
                d.Field("wrapper")
                    .ResolveWith<WrapperQueryResolver>(r => r.GetWrapper(default, default,
                        default));
            })
            .AddMutationType(d =>
            {
                d.Name("Mutation");

                d.Field("wrapper")
                    .Argument("wrapper", d => d.Type<WrapperInputType>())
                    .Argument("order", a =>
                        a.Type<AnyType>())
                    .ResolveWith<WrapperMutationResolver>(r => r.UpsertWrapper(default, default, default));
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