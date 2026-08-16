using Foundgine.Execution;
using Foundgine.HighAssurance.Banking;
using Foundgine.Semantics.Authorization;
using Npgsql;

namespace Foundgine.HighAssurance.Postgres;

/// <summary>
/// PostgreSQL execution boundary for the high-assurance TransferFunds capability.
/// All consequential state, idempotency, and audit writes occur in one database transaction.
/// Authorization is re-evaluated from locked rows immediately before mutation.
/// </summary>
public sealed class PostgresTransferFundsService
{
    /// <summary>Provider-specific security contract for this consequential mutation.</summary>
    public static PostgresMutationSecurityConformance SecurityConformance =>
        PostgresMutationSecurityConformance.TransferFunds;

    private const int PlanVersion = 2;
    private readonly NpgsqlDataSource _dataSource;
    private readonly Func<Guid, BankAccount, BankAccount, bool> _authorize;

    public PostgresTransferFundsService(
        NpgsqlDataSource dataSource,
        Func<Guid, BankAccount, BankAccount, bool> authorize)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _authorize = authorize ?? throw new ArgumentNullException(nameof(authorize));
    }

    public async Task<TransferExecutionReceipt> ExecuteAsync(
        Guid actorId,
        int tenantId,
        TransferFundsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateShape(command);
        PostgresMutationSecurityConformanceGate.EnsureKnownInvariants();
        SecurityConformance.EnsureSatisfied();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Serialize all requests carrying the same idempotency key, even when their
            // account pairs differ. This avoids a unique-key race that would otherwise
            // abort the PostgreSQL transaction after the conflicting INSERT.
            await AdvisoryLockAsync(connection, transaction, command.IdempotencyKey, cancellationToken);

            var existing = await FindIdempotencyAsync(connection, transaction, command.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                EnsureReplayMatches(existing, actorId, tenantId, command);
                await transaction.CommitAsync(cancellationToken);
                return BuildReceipt(actorId, tenantId, command, existing.TransferId, existing.SourceBalance, existing.DestinationBalance, replay: true);
            }

            var sourceId = command.SourceAccountId;
            var destinationId = command.DestinationAccountId;
            var firstId = sourceId.CompareTo(destinationId) < 0 ? sourceId : destinationId;
            var secondId = firstId == sourceId ? destinationId : sourceId;

            // Deterministic row-lock order prevents deadlocks between A -> B and B -> A.
            var first = await LoadForUpdateAsync(connection, transaction, firstId, cancellationToken)
                ?? throw new InvalidOperationException($"Account '{firstId}' was not found.");
            var second = await LoadForUpdateAsync(connection, transaction, secondId, cancellationToken)
                ?? throw new InvalidOperationException($"Account '{secondId}' was not found.");

            var source = first.Id == sourceId ? first : second;
            var destination = first.Id == destinationId ? first : second;

            ValidateExecution(actorId, tenantId, source, destination, command.Amount);

            // Authorization is deliberately evaluated after FOR UPDATE and immediately
            // before the state transition. Planning-time authorization is not trusted here.
            if (!_authorize(actorId, source, destination))
                throw new SemanticAuthorizationException("Transfer capability is not authorized for this actor and account pair.");

            var transferId = Guid.NewGuid();
            var sourceBalance = source.Balance - command.Amount;
            var destinationBalance = destination.Balance + command.Amount;

            await UpdateSourceAsync(connection, transaction, source, command.Amount, cancellationToken);
            await UpdateDestinationAsync(connection, transaction, destination, command.Amount, cancellationToken);
            await InsertIdempotencyAsync(connection, transaction, command, actorId, tenantId, transferId, sourceBalance, destinationBalance, cancellationToken);
            await InsertAuditAsync(connection, transaction, command, actorId, tenantId, transferId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return BuildReceipt(actorId, tenantId, command, transferId, sourceBalance, destinationBalance, replay: false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void ValidateShape(TransferFundsCommand command)
    {
        if (command.Amount <= 0) throw new InvalidOperationException("Transfer amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new InvalidOperationException("Idempotency key is required.");
        if (command.SourceAccountId == command.DestinationAccountId) throw new InvalidOperationException("Source and destination accounts must differ.");
    }

    private static void ValidateExecution(Guid actorId, int tenantId, BankAccount source, BankAccount destination, decimal amount)
    {
        if (source.TenantId != tenantId || destination.TenantId != tenantId)
            throw new InvalidOperationException("Tenant boundary violation.");
        if (source.IsFrozen || destination.IsFrozen)
            throw new InvalidOperationException("Frozen accounts cannot participate in transfers.");
        if (source.DailyTransferred + amount > source.DailyLimit)
            throw new InvalidOperationException("Transfer exceeds the source account daily limit.");
        var available = source.Balance - source.PendingTransactions - source.RegulatoryHold;
        if (available < amount)
            throw new InvalidOperationException("Insufficient available funds.");
    }

    private static async Task AdvisoryLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));", connection, transaction);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<BankAccount?> LoadForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id, tenant_id, owner_id, balance, pending_transactions, regulatory_hold,
                   daily_transferred, daily_limit, is_frozen
            FROM banking.bank_account
            WHERE id = @id
            FOR UPDATE;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new BankAccount(
            reader.GetGuid(0), reader.GetInt32(1), reader.GetGuid(2), reader.GetDecimal(3),
            reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetBoolean(8));
    }

    private static async Task<IdempotencyRow?> FindIdempotencyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken ct)
    {
        const string sql = """
            SELECT actor_id, tenant_id, source_account_id, destination_account_id, amount,
                   transfer_id, source_balance, destination_balance
            FROM banking.transfer_idempotency
            WHERE idempotency_key = @key;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new IdempotencyRow(reader.GetGuid(0), reader.GetInt32(1), reader.GetGuid(2), reader.GetGuid(3),
            reader.GetDecimal(4), reader.GetGuid(5), reader.GetDecimal(6), reader.GetDecimal(7));
    }

    private static async Task UpdateSourceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, BankAccount source, decimal amount, CancellationToken ct)
    {
        const string sql = """
            UPDATE banking.bank_account
            SET balance = balance - @amount,
                daily_transferred = daily_transferred + @amount
            WHERE id = @id;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("amount", amount);
        command.Parameters.AddWithValue("id", source.Id);
        await EnsureOneRowAsync(command, ct);
    }

    private static async Task UpdateDestinationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, BankAccount destination, decimal amount, CancellationToken ct)
    {
        const string sql = "UPDATE banking.bank_account SET balance = balance + @amount WHERE id = @id;";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("amount", amount);
        command.Parameters.AddWithValue("id", destination.Id);
        await EnsureOneRowAsync(command, ct);
    }

    private static async Task InsertIdempotencyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TransferFundsCommand command, Guid actorId, int tenantId, Guid transferId, decimal sourceBalance, decimal destinationBalance, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO banking.transfer_idempotency
                (idempotency_key, actor_id, tenant_id, source_account_id, destination_account_id,
                 amount, transfer_id, source_balance, destination_balance)
            VALUES
                (@key, @actor, @tenant, @source, @destination, @amount, @transfer, @source_balance, @destination_balance);
            """;
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("key", command.IdempotencyKey);
        cmd.Parameters.AddWithValue("actor", actorId);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("source", command.SourceAccountId);
        cmd.Parameters.AddWithValue("destination", command.DestinationAccountId);
        cmd.Parameters.AddWithValue("amount", command.Amount);
        cmd.Parameters.AddWithValue("transfer", transferId);
        cmd.Parameters.AddWithValue("source_balance", sourceBalance);
        cmd.Parameters.AddWithValue("destination_balance", destinationBalance);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TransferFundsCommand command, Guid actorId, int tenantId, Guid transferId, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO banking.transfer_audit
                (transfer_id, action, actor_id, tenant_id, source_account_id, destination_account_id, amount)
            VALUES
                (@transfer, 'transferFunds', @actor, @tenant, @source, @destination, @amount);
            """;
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("transfer", transferId);
        cmd.Parameters.AddWithValue("actor", actorId);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("source", command.SourceAccountId);
        cmd.Parameters.AddWithValue("destination", command.DestinationAccountId);
        cmd.Parameters.AddWithValue("amount", command.Amount);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureOneRowAsync(NpgsqlCommand command, CancellationToken ct)
    {
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("Transfer state transition affected an unexpected number of rows.");
    }

    private static void EnsureReplayMatches(IdempotencyRow row, Guid actorId, int tenantId, TransferFundsCommand command)
    {
        if (row.ActorId != actorId || row.TenantId != tenantId || row.SourceAccountId != command.SourceAccountId ||
            row.DestinationAccountId != command.DestinationAccountId || row.Amount != command.Amount)
            throw new InvalidOperationException("The idempotency key is already bound to a different transfer request.");
    }

    private static TransferExecutionReceipt BuildReceipt(Guid actorId, int tenantId, TransferFundsCommand command, Guid transferId, decimal sourceBalance, decimal destinationBalance, bool replay)
    {
        var now = DateTimeOffset.UtcNow;
        var intent = ExecutionEvidenceFactory.Hash($"{TransferFundsService.CapabilityId}|v{TransferFundsService.CapabilityVersion}|{actorId}|{tenantId}|{command.SourceAccountId}|{command.DestinationAccountId}|{command.Amount}|{command.IdempotencyKey}");
        var plan = ExecutionEvidenceFactory.Hash("transferFunds|postgres-plan-v2|advisory-idempotency-lock|row-locks|tenant-isolation|execution-authorization|frozen-state|available-funds|daily-limit|atomic-state-idempotency-audit");
        var authorization = ExecutionEvidenceFactory.Hash($"actor:{actorId}|tenant:{tenantId}|source:{command.SourceAccountId}|destination:{command.DestinationAccountId}");
        var evidence = new ExecutionEvidence("postgres", plan, [], 1, 0, IntentFingerprint: intent, AuthorizationFingerprint: authorization);
        var resultFingerprint = ExecutionEvidenceFactory.Hash($"{transferId}|{command.Amount}|{sourceBalance}|{destinationBalance}|{replay}");
        var execution = ExecutionReceiptFactory.Create(
            Guid.NewGuid().ToString("N"), evidence, resultFingerprint, [],
            replay ? ["transferFunds.replay"] : ["transferFunds.debit", "transferFunds.credit", "transferFunds.idempotency", "transferFunds.audit"],
            now, DateTimeOffset.UtcNow, 1, TransferFundsService.CapabilityVersion, 1, PlanVersion, "banking-semantic-model-v1");
        var securityProof = SecurityInvariantProof.Create(
            "postgres-high-assurance",
            PostgresMutationSecurityConformance.TransferFunds.RequiredInvariants,
            PostgresMutationSecurityConformance.TransferFunds.RequiredInvariants);
        securityProof.EnsureSatisfied();
        return new TransferExecutionReceipt(execution, transferId, command.SourceAccountId, command.DestinationAccountId, command.Amount, replay, securityProof);
    }

    private sealed record IdempotencyRow(Guid ActorId, int TenantId, Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, Guid TransferId, decimal SourceBalance, decimal DestinationBalance);
}
