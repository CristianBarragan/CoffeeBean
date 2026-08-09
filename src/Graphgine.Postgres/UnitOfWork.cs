using Npgsql;
using System.Data;

namespace Graphgine.Postgres;

public interface IUnitOfWork
{
    void BeginTransaction();

    void RollbackTranscation();

    void CommitTransaction();

    void DisposeConnection();

    bool IsDisposed { get; }

    int NumberOfNestedTransactions { get; }

    NpgsqlConnection NpgsqlConnection { get; }
}

/// <summary>
/// Reference-counted transaction wrapper: BeginTransaction/CommitTransaction
/// can be called re-entrantly (e.g. a mutation that calls into another
/// mutation), and only the outermost Commit/Rollback actually touches the
/// underlying ADO.NET transaction. NumberOfNestedTransactions is that
/// reference count and must be decremented on every matching
/// Commit-or-Rollback call, not just on Commit -- see RollbackTranscation
/// below, which used to leak the count.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _npgsqlConnection;

    private IDbTransaction? _dbTransaction;

    public NpgsqlConnection NpgsqlConnection => _npgsqlConnection;

    public bool IsDisposed { get; private set; } = false;

    public int NumberOfNestedTransactions { get; private set; } = 0;

    public bool InTransaction { get; private set; } = false;

    public UnitOfWork(NpgsqlConnection npgsqlConnection)
    {
        _npgsqlConnection = npgsqlConnection;
        _npgsqlConnection.Open();
    }

    public void BeginTransaction()
    {
        if (NumberOfNestedTransactions == 0)
        {
            _dbTransaction = _npgsqlConnection.BeginTransaction();
            InTransaction = true;
        }

        NumberOfNestedTransactions++;
    }

    public void CommitTransaction()
    {
        if (NumberOfNestedTransactions == 1 && InTransaction)
        {
            _dbTransaction!.Commit();
            InTransaction = false;
        }

        if (NumberOfNestedTransactions > 0)
            NumberOfNestedTransactions--;
    }

    /// <summary>
    /// FIXED: this used to not decrement NumberOfNestedTransactions at all,
    /// so a rollback anywhere but the outermost call left the count
    /// permanently out of sync with reality -- every subsequent
    /// BeginTransaction/CommitTransaction pair in the same UnitOfWork's
    /// lifetime would then be operating against a wrong count (e.g. a
    /// later CommitTransaction that should be the real, outermost commit
    /// would see NumberOfNestedTransactions != 1 and silently skip
    /// committing anything). Now mirrors CommitTransaction: decrement on
    /// every call, only touch the real transaction at the outermost one.
    /// </summary>
    public void RollbackTranscation()
    {
        if (NumberOfNestedTransactions == 1 && InTransaction)
        {
            _dbTransaction!.Rollback();
            InTransaction = false;
        }

        if (NumberOfNestedTransactions > 0)
            NumberOfNestedTransactions--;
    }

    /// <summary>
    /// FIXED: the original condition was `NumberOfNestedTransactions == 0
    /// &amp;&amp; InTransaction`. After a successful Commit/Rollback,
    /// InTransaction is already false and NumberOfNestedTransactions is
    /// already 0, so that condition can never be true post-completion --
    /// the connection/transaction would never actually get disposed. The
    /// real intent is "there's no open transaction left, so it's safe to
    /// tear down" -- i.e. NumberOfNestedTransactions == 0 is the whole
    /// condition; InTransaction should already agree with it, and
    /// disposing _dbTransaction is only meaningful if one was ever created.
    /// </summary>
    public void DisposeConnection()
    {
        if (NumberOfNestedTransactions == 0)
        {
            _dbTransaction?.Dispose();
            _npgsqlConnection.Dispose();
            _npgsqlConnection.Close();
            InTransaction = false;
            IsDisposed = true;
        }
    }
}
