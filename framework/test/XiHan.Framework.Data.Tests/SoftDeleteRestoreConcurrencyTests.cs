// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Exceptions;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 软删仓储恢复路径的行版本乐观锁测试
/// </summary>
/// <remarks>
/// 跑在真实 SQLite 上，挂与生产同口径的软删过滤器、租户读共享过滤器与自动写过滤设置。
/// 过期实体用「先读一份旧副本，再经新副本恢复并修改」构造，不依赖真实并发。
/// </remarks>
public sealed class SoftDeleteRestoreConcurrencyTests : IDisposable
{
    private const string ConfigId = "Main";
    private const long HomeTenantId = 1;
    private const long ActiveTenantId = 2;

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"xihan-restore-lock-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _scope;
    private readonly SqlSugarSoftDeleteRepository<SoftNote, long> _repository;
    private long? _purgeBeforeUpdateId;

    /// <summary>
    /// 建库并挂软删与租户过滤器
    /// </summary>
    public SoftDeleteRestoreConcurrencyTests()
    {
        _scope = new SqlSugarScope(
            new ConnectionConfig
            {
                ConfigId = ConfigId,
                ConnectionString = ConnectionString,
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                MoreSettings = new ConnMoreSettings
                {
                    IsAutoUpdateQueryFilter = true,
                    IsAutoDeleteQueryFilter = true
                }
            },
            client =>
            {
                client.QueryFilter.AddTableFilter<ISoftDelete>(entity => !entity.IsDeleted);
                client.QueryFilter.AddTableFilter<IMultiTenantEntity>(
                    entity => entity.TenantId == 0 ||
                              entity.TenantId == ResolveTenantScopeId());
                client.Aop.OnLogExecuting = (sql, _) => PurgeBeforeUpdate(sql);
            });

        var client = _scope.GetConnectionScope(ConfigId);
        client.CodeFirst.InitTables<SoftNote>();

        _repository = new SqlSugarSoftDeleteRepository<SoftNote, long>(new FixedClientResolver(client));
    }

    private string ConnectionString => $"DataSource={_databasePath};Pooling=False";

    /// <summary>
    /// 用过期实体恢复被拒，期间的修改保留
    /// </summary>
    [Fact]
    public async Task RestoreAsync_StaleEntity_ShouldThrowConflictAndKeepNewerChanges()
    {
        SeedDeleted(1);
        var stale = LoadRaw(1);
        await RestoreAndModifyAsync(1, "newer");

        _ = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.RestoreAsync(stale));

        var stored = LoadRaw(1);
        Assert.False(stored.IsDeleted);
        Assert.Equal("newer", stored.Text);
    }

    /// <summary>
    /// 批量恢复中含过期实体时被拒，期间的修改保留
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_StaleEntity_ShouldThrowConflictAndKeepNewerChanges()
    {
        SeedDeleted(1);
        SeedDeleted(2);
        var stale = new[] { LoadRaw(1), LoadRaw(2) };
        await RestoreAndModifyAsync(1, "newer");

        _ = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.RestoreRangeAsync(stale));

        Assert.Equal("newer", LoadRaw(1).Text);
    }

    /// <summary>
    /// 无外层事务时批量恢复的第二个实体过期，第一个实体的写入随之回滚
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_SecondEntityStale_WithoutTransaction_ShouldRollBackFirst()
    {
        SeedDeleted(1);
        SeedDeleted(2);
        var stale1 = LoadRaw(1);
        var stale2 = LoadRaw(2);
        await RestoreAndModifyAsync(2, "newer");

        _ = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.RestoreRangeAsync([stale1, stale2]));

        Assert.True(LoadRaw(1).IsDeleted);
        Assert.Equal("newer", LoadRaw(2).Text);
    }

    /// <summary>
    /// 自开事务回滚后已写入实体的行版本还原，刷新过期实体后可原批重试
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_OwnedRollback_ShouldRestoreRowVersionsAndAllowRetry()
    {
        SeedDeleted(1);
        SeedDeleted(2);
        var entity1 = LoadRaw(1);
        var stale2 = LoadRaw(2);
        await RestoreAndModifyAsync(2, "newer");

        _ = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.RestoreRangeAsync([entity1, stale2]));

        Assert.Equal(LoadRaw(1).RowVersion, entity1.RowVersion);

        var fresh2 = LoadRaw(2);
        entity1.IsDeleted = true;
        await _repository.RestoreRangeAsync([entity1, fresh2]);

        AssertRestored(LoadRaw(1));
        AssertRestored(LoadRaw(2));
    }

    /// <summary>
    /// 有外层事务时批量恢复不自行提交，由外层决定去留
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_WithOuterTransaction_ShouldLeaveCommitToOuter()
    {
        SeedDeleted(1);
        SeedDeleted(2);
        var client = _scope.GetConnectionScope(ConfigId);
        var notes = new[] { LoadRaw(1), LoadRaw(2) };

        await client.Ado.BeginTranAsync();
        Assert.NotNull(client.Ado.Transaction);
        await _repository.RestoreRangeAsync(notes);
        Assert.NotNull(client.Ado.Transaction);
        await client.Ado.RollbackTranAsync();

        Assert.True(LoadRaw(1).IsDeleted);
        Assert.True(LoadRaw(2).IsDeleted);
    }

    /// <summary>
    /// 同一主键对应多个不同实例时批量恢复被拒，数据库不变
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_DuplicateKeyDistinctInstances_ShouldThrowArgumentException()
    {
        SeedDeleted(1);
        var first = LoadRaw(1);
        var second = LoadRaw(1);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => _repository.RestoreRangeAsync([first, second]));

        Assert.True(LoadRaw(1).IsDeleted);
    }

    /// <summary>
    /// 同一实例重复传入时只恢复一次
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_SameInstanceTwice_ShouldRestoreOnce()
    {
        SeedDeleted(1);
        var note = LoadRaw(1);

        await _repository.RestoreRangeAsync([note, note]);

        AssertRestored(LoadRaw(1));
    }

    /// <summary>
    /// 预读之后、UPDATE 之前行被并发物理删除时按并发冲突拒绝
    /// </summary>
    [Fact]
    public async Task RestoreAsync_RowPurgedBeforeUpdate_ShouldThrowConflict()
    {
        SeedDeleted(1);
        var note = LoadRaw(1);
        _purgeBeforeUpdateId = 1;

        _ = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.RestoreAsync(note));
    }

    /// <summary>
    /// 恢复已加载实体后清空删除审计字段
    /// </summary>
    [Fact]
    public async Task RestoreAsync_LoadedEntity_ShouldRestoreAndClearDeletionFields()
    {
        SeedDeleted(1);
        var note = LoadRaw(1);

        await _repository.RestoreAsync(note);

        AssertRestored(LoadRaw(1));
    }

    /// <summary>
    /// 按主键恢复后清空删除审计字段
    /// </summary>
    [Fact]
    public async Task RestoreAsync_ById_ShouldRestoreAndClearDeletionFields()
    {
        SeedDeleted(1);

        await _repository.RestoreAsync(1);

        AssertRestored(LoadRaw(1));
    }

    /// <summary>
    /// 批量恢复已加载实体后全部清空删除审计字段
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_LoadedEntities_ShouldRestoreAll()
    {
        SeedDeleted(1);
        SeedDeleted(2);

        await _repository.RestoreRangeAsync([LoadRaw(1), LoadRaw(2)]);

        AssertRestored(LoadRaw(1));
        AssertRestored(LoadRaw(2));
    }

    /// <summary>
    /// 恢复后用同一实体继续更新成功
    /// </summary>
    [Fact]
    public async Task RestoreAsync_ThenUpdateSameEntity_ShouldSucceed()
    {
        SeedDeleted(1);
        var note = LoadRaw(1);

        await _repository.RestoreAsync(note);
        note.Text = "after-restore";
        _ = await _repository.UpdateAsync(note);

        var stored = LoadRaw(1);
        Assert.False(stored.IsDeleted);
        Assert.Equal("after-restore", stored.Text);
    }

    /// <summary>
    /// 租户上下文内恢复异租户行被预读拦下
    /// </summary>
    [Fact]
    public async Task RestoreAsync_ForeignTenantRow_ShouldThrowAndKeepDeleted()
    {
        SeedDeleted(1, HomeTenantId);
        var note = LoadRaw(1);

        using (new TenantScope(ActiveTenantId))
        {
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.RestoreAsync(note));
        }

        Assert.True(LoadRaw(1).IsDeleted);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        AsyncLocalCurrentTenantAccessor.Instance.Current = null;
        _scope.Dispose();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static long ResolveTenantScopeId()
    {
        return AsyncLocalCurrentTenantAccessor.Instance.Current?.TenantId ?? 0;
    }

    private static void AssertRestored(SoftNote note)
    {
        Assert.False(note.IsDeleted);
        Assert.Null(note.DeletedTime);
        Assert.Equal(0, note.DeletedId);
        Assert.Null(note.DeletedBy);
    }

    private void PurgeBeforeUpdate(string sql)
    {
        if (_purgeBeforeUpdateId is not { } id ||
            !sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _purgeBeforeUpdateId = null;
        using var other = CreateRawClient();
        _ = other.Deleteable<SoftNote>()
            .Where(note => note.BasicId == id)
            .ExecuteCommand();
    }

    private async Task RestoreAndModifyAsync(long id, string text)
    {
        await _repository.RestoreAsync(id);
        var fresh = LoadRaw(id);
        fresh.Text = text;
        _ = await _repository.UpdateAsync(fresh);
    }

    private void SeedDeleted(long id, long tenantId = 0)
    {
        using var client = CreateRawClient();
        _ = client.Insertable(new SoftNote(id)
        {
            TenantId = tenantId,
            Text = $"note-{id}",
            IsDeleted = true,
            DeletedTime = DateTimeOffset.UtcNow,
            DeletedId = 7,
            DeletedBy = "alice"
        }).ExecuteCommand();
    }

    private SoftNote LoadRaw(long id)
    {
        using var client = CreateRawClient();
        return client.Queryable<SoftNote>()
            .Where(note => note.BasicId == id)
            .First();
    }

    private SqlSugarClient CreateRawClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = ConnectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    /// <summary>测试用软删多租户实体。</summary>
    [SugarTable(TableName = "Soft_Restore_Note")]
    public class SoftNote : SugarMultiTenantDeletionEntity<long>
    {
        /// <summary>构造函数。</summary>
        public SoftNote()
        {
        }

        /// <summary>构造函数。</summary>
        /// <param name="basicId">主键</param>
        public SoftNote(long basicId) : base(basicId)
        {
        }

        /// <summary>内容。</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>把请求流切进指定租户的作用域。</summary>
    private sealed class TenantScope : IDisposable
    {
        private readonly BasicTenantInfo? _previous;

        public TenantScope(long tenantId)
        {
            _previous = AsyncLocalCurrentTenantAccessor.Instance.Current;
            AsyncLocalCurrentTenantAccessor.Instance.Current = new BasicTenantInfo(tenantId);
        }

        public void Dispose()
        {
            AsyncLocalCurrentTenantAccessor.Instance.Current = _previous;
        }
    }

    /// <summary>固定返回同一客户端的解析器替身。</summary>
    private sealed class FixedClientResolver(ISqlSugarClient client) : ISqlSugarClientResolver
    {
        /// <summary>
        /// 获取当前客户端
        /// </summary>
        public ISqlSugarClient GetCurrentClient() => client;

        /// <summary>
        /// 获取实体对应客户端
        /// </summary>
        /// <param name="entityType">实体类型</param>
        public ISqlSugarClient GetClientForEntity(Type entityType) => client;

        /// <summary>
        /// 按连接配置标识获取客户端
        /// </summary>
        /// <param name="configId">连接配置标识</param>
        public ISqlSugarClient GetClient(string configId) => client;

        /// <summary>
        /// 获取全部连接配置标识
        /// </summary>
        public IReadOnlyCollection<string> GetAllConfigIds() => [ConfigId];

        /// <summary>
        /// 获取当前布局的连接配置标识
        /// </summary>
        public IReadOnlyList<string> GetCurrentLayoutConfigIds() => [ConfigId];

        /// <summary>
        /// 获取全部客户端
        /// </summary>
        public IEnumerable<ISqlSugarClient> GetAllClients() => [client];

        /// <summary>
        /// 底层多库容器，本用例不涉及
        /// </summary>
        public ITenant AsTenant() => throw new NotSupportedException("用例不涉及多库切换。");
    }
}
