using Npgsql;
using System.Data;

namespace Graphgine.Postgres;

public interface IUnitOfWorkContext
{
    IUnitOfWork CreateUnitOfWork();

    NpgsqlConnection GetConnection();
}

public class UnitOfWorkContext : IUnitOfWorkContext
{
    private readonly NpgsqlConnection _npgsqlConnection;

    private IUnitOfWork? _unitOfWork;

    public UnitOfWorkContext(NpgsqlConnection npgsqlConnection)
    {
        _npgsqlConnection = npgsqlConnection;
    }

    /// <summary>
    /// FIXED: the original check was `if (_npgsqlConnection.State == Open)
    /// return _unitOfWork;` with no null check -- on the very first call,
    /// if something else had already opened the connection before this
    /// context ever created a UnitOfWork, this returned null instead of
    /// creating one. Reuse now requires an existing UnitOfWork, not just
    /// an open connection.
    /// </summary>
    public IUnitOfWork CreateUnitOfWork()
    {
        if (_unitOfWork is not null && _npgsqlConnection.State == ConnectionState.Open)
            return _unitOfWork;

        _unitOfWork = new UnitOfWork(_npgsqlConnection);

        return _unitOfWork;
    }

    /// <summary>
    /// FIXED: previously routed through `_unitOfWork!.NpgsqlConnection`,
    /// which threw a NullReferenceException if called before
    /// CreateUnitOfWork() -- the null-forgiving `!` just suppressed the
    /// compiler warning about a real, reachable null. This context already
    /// holds the connection directly, so there's no need to go through
    /// UnitOfWork for it at all.
    /// </summary>
    public NpgsqlConnection GetConnection() => _npgsqlConnection;
}
