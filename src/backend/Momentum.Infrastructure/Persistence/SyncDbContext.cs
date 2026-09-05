using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Momentum.Infrastructure.Persistence;

/// <summary>
/// Persistence context for the sync tables (GOREV slice-2b1 D2). Writes/reads flow through RAW SQL in
/// the <c>Sync/*</c> ports (sharing the current connection/transaction); the DbSets/model exist for
/// migrations and model validation. <c>commit_xid</c>/<c>server_seq</c> are unmapped (DB-generated).
/// </summary>
public sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ProcessedOperation> ProcessedOperations => Set<ProcessedOperation>();

    public DbSet<SyncClientClockRow> SyncClientClocks => Set<SyncClientClockRow>();

    public DbSet<SyncScalarMeta> SyncScalarMeta => Set<SyncScalarMeta>();

    public DbSet<SyncOrsetTag> SyncOrsetTags => Set<SyncOrsetTag>();

    public DbSet<SyncOrsetRemove> SyncOrsetRemoves => Set<SyncOrsetRemove>();

    public DbSet<SyncGcState> SyncGcState => Set<SyncGcState>();

    public DbSet<UserResyncHorizon> UserResyncHorizons => Set<UserResyncHorizon>();

    public DbSet<TaskRow> Tasks => Set<TaskRow>();

    public DbSet<TaskListRow> TaskLists => Set<TaskListRow>();

    public DbSet<ProjectRow> Projects => Set<ProjectRow>();

    public DbSet<TaskTagRow> TaskTags => Set<TaskTagRow>();

    public DbSet<ProjectMemberRow> ProjectMembers => Set<ProjectMemberRow>();

    // IS-EMRI-o83 D1: plain CRUD tables (Infrastructure/Auth/*Store.cs), same DbContext as Sync's
    // (one connection string, one migration history -- a second DbContext would double both for no gain).
    public DbSet<UserRow> Users => Set<UserRow>();

    public DbSet<RefreshTokenRow> RefreshTokens => Set<RefreshTokenRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SyncDbContext).Assembly);
    }

    /// <summary>A raw command bound to the shared connection and the current transaction (if any).</summary>
    public async Task<NpgsqlCommand> CreateRawCommandAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (Database.CurrentTransaction is { } transaction)
        {
            command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        }

        return command;
    }
}
