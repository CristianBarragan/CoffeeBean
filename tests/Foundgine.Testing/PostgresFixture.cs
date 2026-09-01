using Npgsql;
using Xunit;

namespace Foundgine.Testing;

/// <summary>
/// Shared PostgreSQL lifecycle for integration tests.
/// The database is external (normally the repository Docker fixture); tests own
/// rows, not schema creation. Each test gets a fresh connection and the canonical
/// datasets are reset deterministically before use.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionEnvironmentVariable = "FOUNDGINE_POSTGRES_CONNECTION_STRING";
    private const string DefaultConnectionString =
        "Host=localhost;Port=55432;Database=foundgine_e2e;Username=foundgine;Password=foundgine";

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)
            ?? DefaultConnectionString;

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)))
            return;

        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Clears and reseeds the canonical relational workload.</summary>
    public async Task ResetCanonicalQueryDataAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("PostgreSQL connection must be open.");

        await using var command = new NpgsqlCommand("""
            SET search_path TO fg_query;
            TRUNCATE TABLE "Transaction", "Account", "Customer" RESTART IDENTITY CASCADE;

            INSERT INTO "Customer" ("Id", "Name") VALUES
                (1, 'Alice'),
                (2, 'Bob'),
                (3, 'Carol');

            INSERT INTO "Account" ("Id", "CustomerId", "Balance", "Status") VALUES
                (10, 1, 100.50, 'Active'),
                (11, 1, 25.00, 'Frozen'),
                (20, 2, 200.00, 'Active'),
                (30, 3, 0.00, 'Closed');

            INSERT INTO "Transaction" ("Id", "AccountId", "Amount", "TransactionDate") VALUES
                (100, 10, 25.00, '2026-01-01'),
                (101, 10, 75.50, '2026-01-02'),
                (110, 11, 5.00, '2026-01-03'),
                (200, 20, 50.00, '2026-01-04');
            """, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

}

[CollectionDefinition("Foundgine PostgreSQL")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Foundgine PostgreSQL";
}
