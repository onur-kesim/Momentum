using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Momentum.Application.Abstractions.Sync;
using Momentum.Application.Features.Sync;
using Momentum.Domain.Sync;
using Momentum.Infrastructure;
using Momentum.Infrastructure.Persistence;
using Npgsql;
using Xunit;
using EntityState = Momentum.Domain.Sync.EntityState;

// xid/xmin are cluster-global: parallel test classes would corrupt each other's horizon (D9 pin).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Momentum.Persistence.Tests;

/// <summary>One shared postgres:17-alpine container for the whole assembly (GOREV slice-2b1 D9).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public Testcontainers.PostgreSql.PostgreSqlContainer Container { get; } =
        new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => Container.StartAsync();

    public async Task DisposeAsync() => await Container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>Creates a fresh migrated database per test (isolation) inside the shared container.</summary>
public static class TestDatabase
{
    public static async Task<string> CreateAsync(PostgresFixture fixture)
    {
        var databaseName = "t_" + Guid.NewGuid().ToString("N");

        await using (var admin = new NpgsqlConnection(fixture.Container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        // IS-EMRI-o83-B s2.1 (Onur kilidi, 18 Agu -- Cowork'un cihazda olctugu havuz sizintisi):
        // her test KENDI t_<guid> veritabanina acilan, assembly sonuna kadar canli kalan en az bir
        // bosta Npgsql baglantisi birakiyordu (DisableTestParallelization=true ⇒ gercek eszamanli
        // ihtiyac YOK, ama global connection-string-anahtarli havuz hicbir yerde budanmiyordu).
        // max_connections ARTIRILMAZ (karar: tavan yukseltmek sizintiyi test sayisiyla dogrusal
        // buyutur) -- havuz KAYNAGINDA kelepcelenir, TEK cagri yerinde.
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.Container.GetConnectionString())
        {
            Database = databaseName,
            MaxPoolSize = 4, // sirali kosumda fazlasi gerekmiyor
            ConnectionIdleLifetime = 1, // saniye -- bosta baglanti budanir
            ConnectionPruningInterval = 1,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<SyncDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new SyncDbContext(options);
        await db.Database.MigrateAsync();

        return connectionString;
    }
}

/// <summary>
/// A minimal service provider around <see cref="DependencyInjection.AddSyncInfrastructure"/> so tests can
/// run the real <see cref="SyncCommandHandler"/> and ports against a database.
/// </summary>
public sealed class SyncTestApp : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public SyncTestApp(string connectionString, TimeProvider? timeProvider = null, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSyncInfrastructure(connectionString);
        configure?.Invoke(services); // e.g. replace a port with a fault-injecting decorator
        _provider = services.BuildServiceProvider();
    }

    /// <summary>Runs one /v1/sync command in its own DI scope (fresh DbContext/connection).</summary>
    public async Task<SyncResponse> SyncAsync(Guid actorId, SyncRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<SyncCommandHandler>(scope.ServiceProvider);
        return await handler.Handle(new SyncCommand(actorId, request), cancellationToken);
    }

    public async Task<SnapshotPage> SnapshotAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISyncPuller>().SnapshotAsync(actorId, cancellationToken);
    }

    public async Task<PullPage> PullAsync(Guid actorId, SyncCursor since, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISyncPuller>().PullIncrementalAsync(actorId, since, cancellationToken);
    }

    public async Task<bool> ShouldResyncAsync(Guid actorId, SyncCursor since, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISyncPuller>().ShouldResyncAsync(actorId, since, cancellationToken);
    }

    public async Task<Hlc?> GetClientClockAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IClientClock>().GetAsync(clientId, cancellationToken);
    }

    /// <summary>Hydrate an entity from persistence into a Domain <see cref="EntityState"/> (for C4 assertions).</summary>
    public async Task<EntityState> HydrateAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        var state = new EntityState();
        await scope.ServiceProvider.GetRequiredService<ISyncStore>().HydrateAsync(state, entityType, entityId, cancellationToken);
        return state;
    }

    /// <summary>
    /// slice-2c D3: calls <c>ISyncStore.PersistDeltaAsync</c> DIRECTLY on a caller-supplied (typically
    /// UNHYDRATED) <see cref="EntityState"/> -- no <c>LockEntityAsync</c>, no <c>HydrateAsync</c>. Proves
    /// the storage-level GREATEST on <c>sync_orset_tags.hlc</c> holds even when a caller skipped the
    /// normal lock+hydrate discipline (the D0/D0-b pattern, repeated for the OR-Set tag column).
    /// </summary>
    public async Task PersistSetDeltaWithoutHydrationAsync(ChangeOperation op, EntityState state, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISyncStore>().PersistDeltaAsync(op, state, cancellationToken);
    }

    /// <summary>
    /// slice-2b2 D0: calls <c>IClientClock.UpsertGreatestAsync</c> DIRECTLY, WITHOUT the client advisory
    /// lock (no <c>LockClientAsync</c> call) -- simulates a future "forgot the lock" code path to prove
    /// the storage-level GREATEST invariant holds independent of caller discipline.
    /// </summary>
    public async Task UpsertClientClockGreatestAsync(Guid clientId, Hlc candidate, CancellationToken cancellationToken = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IClientClock>().UpsertGreatestAsync(clientId, candidate, cancellationToken);
    }

    /// <summary>
    /// slice-2b2 D0-b: opens a scope + an EXPLICIT, held-open transaction and returns a handle whose
    /// <c>UpsertGreatestAsync</c> runs inside it (not committed until <see cref="HeldClientClockScope.CommitAsync"/>).
    /// "Altyapı notu" (D0-b): no such "hold a txn/scope open across an await" helper existed before this slice.
    /// </summary>
    public async Task<HeldClientClockScope> BeginClientClockScopeAsync(CancellationToken cancellationToken = default)
    {
        var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var clock = scope.ServiceProvider.GetRequiredService<IClientClock>();
        return new HeldClientClockScope(scope, transaction, clock);
    }

    public ValueTask DisposeAsync()
    {
        _provider.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>See <see cref="SyncTestApp.BeginClientClockScopeAsync"/> (GOREV slice-2b2 D0-b).</summary>
public sealed class HeldClientClockScope : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;
    private readonly IClientClock _clock;
    private bool _committed;

    internal HeldClientClockScope(AsyncServiceScope scope, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, IClientClock clock)
    {
        _scope = scope;
        _transaction = transaction;
        _clock = clock;
    }

    public Task UpsertGreatestAsync(Guid clientId, Hlc candidate, CancellationToken cancellationToken = default) =>
        _clock.UpsertGreatestAsync(clientId, candidate, cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await _transaction.RollbackAsync();
        }

        await _transaction.DisposeAsync();
        await _scope.DisposeAsync();
    }
}

/// <summary>Small builders for wire ops (Task-centric) used across the gate tests.</summary>
public static class Wire
{
    public const long BaseWall = 1_700_000_000_000;

    public static WireHlc Hlc(Guid clientId, uint counter = 0, long wallOffset = 0) =>
        new(BaseWall + wallOffset, counter, clientId);

    /// <summary>3-part codec of an HLC (for seeding raw outbox rows in hazard tests).</summary>
    public static string EncodeHlc(Guid clientId, uint counter) => $"{BaseWall:D13}.{counter:x8}.{clientId:N}";

    public static WireOp TaskField(Guid op, Guid client, Guid entity, Guid actor, string field, string? value, uint counter = 0, long wallOffset = 0) =>
        new(op, client, entity, actor, "Task", Hlc(client, counter, wallOffset),
            new Dictionary<string, WireFieldWrite> { [field] = new(value, Hlc(client, counter, wallOffset)) },
            null, null, null);

    public static SyncRequest PushOnly(Guid clientId, params WireOp[] ops) => new(clientId, null, null, ops);

    // Push without a pull side (incremental from a very high cursor returns nothing).
    public static SyncRequest PushNoPull(Guid clientId, params WireOp[] ops) =>
        new(clientId, null, new WireCursor(9_000_000_000_000_000_000UL, 0), ops);

    public static WireOp TaskFields(Guid op, Guid client, Guid entity, Guid actor, uint counter, params (string Field, string? Value)[] fields) =>
        new(op, client, entity, actor, "Task", Hlc(client, counter),
            fields.ToDictionary(f => f.Field, f => new WireFieldWrite(f.Value, Hlc(client, counter)), StringComparer.Ordinal),
            null, null, null);

    public static WireOp TaskGroup(Guid op, Guid client, Guid entity, Guid actor, uint counter, params (string Member, string? Value)[] members) =>
        new(op, client, entity, actor, "Task", Hlc(client, counter), null, null,
            new Dictionary<string, WireGroupWrite>(StringComparer.Ordinal)
            {
                ["completion"] = new(members.ToDictionary(m => m.Member, m => m.Value, StringComparer.Ordinal), Hlc(client, counter)),
            },
            null);

    public static WireOp TaskSet(Guid op, Guid client, Guid entity, Guid actor, uint counter, IReadOnlyList<WireSetAdd>? adds, IReadOnlyList<WireSetRemove>? removes) =>
        new(op, client, entity, actor, "Task", Hlc(client, counter), null,
            new Dictionary<string, WireSetDelta>(StringComparer.Ordinal) { ["tags"] = new(adds, removes) },
            null, null);

    public static WireOp Op(Guid op, Guid client, Guid entity, Guid actor, uint counter,
        IReadOnlyDictionary<string, WireFieldWrite>? fields = null,
        IReadOnlyDictionary<string, WireSetDelta>? sets = null,
        string entityType = "Task") =>
        new(op, client, entity, actor, entityType, Hlc(client, counter), fields, sets, null, null);
}
