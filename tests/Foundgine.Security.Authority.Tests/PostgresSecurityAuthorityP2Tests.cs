using Foundgine.HighAssurance.Banking;
using Foundgine.HighAssurance.Postgres;
using Foundgine.HighAssurance.Postgres.Execution;
using Npgsql;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

/// <summary>
/// P2 database-authority conformance tests. These deliberately exercise the real
/// PostgreSQL transaction boundary rather than mocks or in-memory state.
/// </summary>
public sealed class PostgresSecurityAuthorityP2Tests
{
    private readonly string _connectionString = PostgresTestEnvironment.ConnectionString;

    [PostgresFact]
    public async Task Cross_tenant_mutation_is_rejected_and_database_is_unchanged()
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await PrepareAsync(dataSource);
        var actor = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        await SeedAsync(dataSource, 700, actor, source, destination, destinationTenant: 701);

        var service = CreateService(dataSource);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            actor, 700, new TransferFundsCommand(source, destination, 10m, "p2-cross-tenant")));

        var state = await ReadStateAsync(dataSource, source, destination);
        Assert.Equal(100m, state.SourceBalance);
        Assert.Equal(50m, state.DestinationBalance);
        Assert.Equal(0, state.IdempotencyCount);
        Assert.Equal(0, state.AuditCount);
    }

    [PostgresFact]
    public async Task Ownership_authorization_is_revalidated_by_the_execution_boundary()
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await PrepareAsync(dataSource);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        await SeedAsync(dataSource, 701, owner, source, destination);

        var service = new PostgresTransferFundsService(
            new PostgresTransferFundsExecutor(dataSource, static (actor, a, b) =>
                actor == a.OwnerId && actor == b.OwnerId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            attacker, 701, new TransferFundsCommand(source, destination, 10m, "p2-owner")));

        var state = await ReadStateAsync(dataSource, source, destination);
        Assert.Equal(100m, state.SourceBalance);
        Assert.Equal(50m, state.DestinationBalance);
        Assert.Equal(0, state.IdempotencyCount);
        Assert.Equal(0, state.AuditCount);
    }

    [PostgresFact]
    public async Task Mutation_invariant_failure_rolls_back_every_side_effect()
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await PrepareAsync(dataSource);
        var actor = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        await SeedAsync(dataSource, 702, actor, source, destination, dailyLimit: 5m);

        var service = CreateService(dataSource);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            actor, 702, new TransferFundsCommand(source, destination, 10m, "p2-limit")));

        var state = await ReadStateAsync(dataSource, source, destination);
        Assert.Equal(100m, state.SourceBalance);
        Assert.Equal(50m, state.DestinationBalance);
        Assert.Equal(0, state.IdempotencyCount);
        Assert.Equal(0, state.AuditCount);
    }

    [PostgresFact]
    public async Task Idempotency_is_database_authoritative_and_replay_does_not_mutate_again()
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await PrepareAsync(dataSource);
        var actor = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        await SeedAsync(dataSource, 703, actor, source, destination);
        var service = CreateService(dataSource);
        var command = new TransferFundsCommand(source, destination, 10m, "p2-replay");

        var first = await service.ExecuteAsync(actor, 703, command);
        var replay = await service.ExecuteAsync(actor, 703, command);

        Assert.False(first.Replay);
        Assert.True(replay.Replay);
        Assert.Equal(first.TransferId, replay.TransferId);
        var state = await ReadStateAsync(dataSource, source, destination);
        Assert.Equal(90m, state.SourceBalance);
        Assert.Equal(60m, state.DestinationBalance);
        Assert.Equal(1, state.IdempotencyCount);
        Assert.Equal(1, state.AuditCount);
    }

    [PostgresFact]
    public async Task Concurrent_same_key_requests_linearize_to_one_database_mutation()
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await PrepareAsync(dataSource);
        var actor = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        await SeedAsync(dataSource, 704, actor, source, destination);
        var service = CreateService(dataSource);
        var command = new TransferFundsCommand(source, destination, 10m, "p2-concurrent");

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => service.ExecuteAsync(actor, 704, command)));

        Assert.Equal(1, results.Count(x => !x.Replay));
        Assert.Equal(7, results.Count(x => x.Replay));
        var state = await ReadStateAsync(dataSource, source, destination);
        Assert.Equal(90m, state.SourceBalance);
        Assert.Equal(60m, state.DestinationBalance);
        Assert.Equal(1, state.IdempotencyCount);
        Assert.Equal(1, state.AuditCount);
    }

    [PostgresFact]
    public async Task Frozen_destination_is_rejected_without_partial_write()
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await PrepareAsync(dataSource);
        var actor = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        await SeedAsync(dataSource, 705, actor, source, destination, destinationFrozen: true);
        var service = CreateService(dataSource);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            actor, 705, new TransferFundsCommand(source, destination, 10m, "p2-frozen")));

        var state = await ReadStateAsync(dataSource, source, destination);
        Assert.Equal(100m, state.SourceBalance);
        Assert.Equal(50m, state.DestinationBalance);
        Assert.Equal(0, state.IdempotencyCount);
        Assert.Equal(0, state.AuditCount);
    }

    private static PostgresTransferFundsService CreateService(NpgsqlDataSource dataSource) =>
        new(new PostgresTransferFundsExecutor(dataSource, static (actor, a, b) =>
            actor == a.OwnerId && actor == b.OwnerId));

    private static async Task PrepareAsync(NpgsqlDataSource dataSource)
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "schema.sql"));
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
        await using var clear = dataSource.CreateCommand(
            "TRUNCATE banking.transfer_audit, banking.transfer_idempotency, banking.authorization_context_tombstone, banking.authorization_context, banking.authorization_context_writer, banking.bank_account;");
        await clear.ExecuteNonQueryAsync();
    }

    private static async Task SeedAsync(
        NpgsqlDataSource dataSource, int tenant, Guid owner, Guid source, Guid destination,
        int? destinationTenant = null, bool destinationFrozen = false, decimal dailyLimit = 1_000_000m)
    {
        const string sql = """
            INSERT INTO banking.bank_account
                (id, tenant_id, owner_id, balance, pending_transactions, regulatory_hold, daily_transferred, daily_limit, is_frozen)
            VALUES
                (@source, @tenant, @owner, 100, 0, 0, 0, @limit, false),
                (@destination, @destinationTenant, @owner, 50, 0, 0, 0, @limit, @frozen);
            """;
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("source", source);
        command.Parameters.AddWithValue("destination", destination);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("destinationTenant", destinationTenant ?? tenant);
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("limit", dailyLimit);
        command.Parameters.AddWithValue("frozen", destinationFrozen);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<State> ReadStateAsync(NpgsqlDataSource dataSource, Guid source, Guid destination)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        const string sql = """
            SELECT
                (SELECT balance FROM banking.bank_account WHERE id = @source),
                (SELECT balance FROM banking.bank_account WHERE id = @destination),
                (SELECT count(*) FROM banking.transfer_idempotency),
                (SELECT count(*) FROM banking.transfer_audit);
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("source", source);
        command.Parameters.AddWithValue("destination", destination);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new State(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetInt64(2), reader.GetInt64(3));
    }

    private sealed record State(decimal SourceBalance, decimal DestinationBalance, long IdempotencyCount, long AuditCount);
}
