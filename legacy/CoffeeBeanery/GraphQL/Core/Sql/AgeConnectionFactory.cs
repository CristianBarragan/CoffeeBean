using Npgsql;

namespace CoffeeBeanery.GraphQL.Core.Sql;

public static class AgeConnectionFactory
{
    public static async Task<NpgsqlConnection> OpenAsync(NpgsqlDataSource ds)
    {
        var conn = await ds.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                              LOAD 'age';
                              SET search_path = ag_catalog, public;
                          """;

        await cmd.ExecuteNonQueryAsync();

        return conn;
    }
}