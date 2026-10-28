using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace DynamicCube.Platform.Persistence;

/// <summary>
/// Runs work that spans modules in one database transaction (e.g. a workflow action and the request status it sets).
/// Every module context of a DI scope uses the scope's <see cref="ModuleConnection"/>; while a unit of work is open,
/// each context joins its transaction when it saves.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Starts a transaction, or joins the one already open in this scope (the outermost scope commits).</summary>
    Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken);
}

public interface IUnitOfWorkScope : IAsyncDisposable
{
    /// <summary>Commits when this is the outermost scope; otherwise the outermost scope decides.</summary>
    Task CommitAsync(CancellationToken cancellationToken);
}

/// <summary>The database connection shared by the module contexts of one DI scope.</summary>
public sealed class ModuleConnection : IAsyncDisposable, IDisposable
{
    private static readonly ConditionalWeakTable<DbConnection, ModuleConnection> ByConnection = [];
    private readonly List<DbContext> _enlisted = [];

    public ModuleConnection(string connectionString)
    {
        Connection = new NpgsqlConnection(connectionString);
        ByConnection.Add(Connection, this);
    }

    public NpgsqlConnection Connection { get; }

    internal NpgsqlTransaction? Transaction { get; private set; }

    internal static ModuleConnection? Of(DbConnection connection) => ByConnection.TryGetValue(connection, out var owner) ? owner : null;

    /// <summary>Makes <paramref name="context"/> use the open unit-of-work transaction, if any and not already.</summary>
    internal async Task EnlistAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (Transaction is null || context.Database.CurrentTransaction is not null)
        {
            return;
        }

        await context.Database.UseTransactionAsync(Transaction, cancellationToken);
        _enlisted.Add(context);
    }

    internal async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (Connection.State != System.Data.ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken);
        }

        Transaction = await Connection.BeginTransactionAsync(cancellationToken);
    }

    internal async Task EndAsync(bool commit, CancellationToken cancellationToken)
    {
        var transaction = Transaction!;
        Transaction = null;
        try
        {
            if (commit)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
        }
        finally
        {
            foreach (var context in _enlisted)
            {
                await context.Database.UseTransactionAsync(null, CancellationToken.None);
            }

            _enlisted.Clear();
            await transaction.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
        {
            await EndAsync(commit: false, CancellationToken.None);
        }

        await Connection.DisposeAsync();
    }

    public void Dispose()
    {
        Transaction?.Dispose();
        Transaction = null;
        Connection.Dispose();
    }
}

internal sealed class UnitOfWork(ModuleConnection connection) : IUnitOfWork
{
    public async Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken)
    {
        if (connection.Transaction is not null)
        {
            return new Scope(null);
        }

        await connection.BeginAsync(cancellationToken);
        return new Scope(connection);
    }

    /// <summary>The outermost scope owns the transaction; inner scopes only join it.</summary>
    private sealed class Scope(ModuleConnection? owner) : IUnitOfWorkScope
    {
        private bool _done;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            if (owner is not null && !_done)
            {
                _done = true;
                await owner.EndAsync(commit: true, cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (owner is not null && !_done)
            {
                _done = true;
                await owner.EndAsync(commit: false, CancellationToken.None);
            }
        }
    }
}

/// <summary>Joins the scope's unit of work before a module context saves.</summary>
internal static class UnitOfWorkEnlistment
{
    public static Task EnlistAsync(DbContext context, CancellationToken cancellationToken) =>
        ModuleConnection.Of(context.Database.GetDbConnection()) is { } owner ? owner.EnlistAsync(context, cancellationToken) : Task.CompletedTask;
}
