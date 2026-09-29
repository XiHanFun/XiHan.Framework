// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 软删仓储物理清除测试
/// </summary>
/// <remarks>
/// 跑在真实 SQLite 上，挂与生产同口径的软删过滤器、租户读共享过滤器与自动写过滤设置。
/// 并发恢复用例在 DELETE 语句执行前经另一个客户端恢复目标行并提交，复现「预读 → 恢复提交 → DELETE」的交错。
/// </remarks>
public sealed class SoftDeletePurgeTests : IDisposable
{
    private const string ConfigId = "Main";
    private const long HomeTenantId = 1;
    private const long ActiveTenantId = 2;

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"xihan-purge-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _scope;
    private readonly SqlSugarSoftDeleteRepository<SoftNote, long> _repository;
    private long? _restoreBeforeDeleteId;

    /// <summary>
    /// 建库、挂软删与租户过滤器、在 DELETE 执行前挂交错恢复钩子
    /// </summary>
    public SoftDeletePurgeTests()
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
                client.Aop.OnLogExecuting = (sql, _) => RestoreBeforeDelete(sql);
            });

        var client = _scope.GetConnectionScope(ConfigId);
        client.CodeFirst.InitTables<SoftNote>();

        _repository = new SqlSugarSoftDeleteRepository<SoftNote, long>(new FixedClientResolver(client));
    }

    private string ConnectionString => $"DataSource={_databasePath};Pooling=False";

    /// <summary>
    /// 已软删的行被物理清除
    /// </summary>
    [Fact]
    public async Task PurgeAsync_DeletedRow_ShouldDelete()
    {
        Seed(1, isDeleted: true);

        var purged = await _repository.PurgeAsync(1);

        Assert.True(purged);
        Assert.Null(LoadRaw(1));
    }

    /// <summary>
    /// 活动数据在预读阶段即被拒绝
    /// </summary>
    [Fact]
    public async Task PurgeAsync_ActiveRow_ShouldThrow()
    {
        Seed(1, isDeleted: false);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.PurgeAsync(1));

        Assert.NotNull(LoadRaw(1));
    }

    /// <summary>
    /// 预读之后、DELETE 之前被并发恢复的行不被清除，调用方收到拒绝
    /// </summary>
    [Fact]
    public async Task PurgeAsync_RowRestoredBeforeDelete_ShouldThrowAndKeepRow()
    {
        Seed(1, isDeleted: true);
        _restoreBeforeDeleteId = 1;

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.PurgeAsync(1));

        var stored = LoadRaw(1);
        Assert.NotNull(stored);
        Assert.False(stored.IsDeleted);
    }

    /// <summary>
    /// 批量清除时被并发恢复的行保留，其余行照常清除
    /// </summary>
    [Fact]
    public async Task PurgeRangeAsync_OneRowRestoredBeforeDelete_ShouldDeleteOthersOnly()
    {
        Seed(1, isDeleted: true);
        Seed(2, isDeleted: true);
        _restoreBeforeDeleteId = 1;

        var purgedCount = await _repository.PurgeRangeAsync([1, 2]);

        Assert.Equal(1, purgedCount);
        var restored = LoadRaw(1);
        Assert.NotNull(restored);
        Assert.False(restored.IsDeleted);
        Assert.Null(LoadRaw(2));
    }

    /// <summary>
    /// 异租户的已软删行不可见，清除返回 false 且行保留
    /// </summary>
    [Fact]
    public async Task PurgeAsync_ForeignTenantRow_ShouldReturnFalseAndKeepRow()
    {
        Seed(1, isDeleted: true, tenantId: HomeTenantId);

        bool purged;
        using (new TenantScope(ActiveTenantId))
        {
            purged = await _repository.PurgeAsync(1);
        }

        Assert.False(purged);
        Assert.NotNull(LoadRaw(1));
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

    private void RestoreBeforeDelete(string sql)
    {
        if (_restoreBeforeDeleteId is not { } id ||
            !sql.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _restoreBeforeDeleteId = null;
        using var other = CreateRawClient();
        _ = other.Updateable<SoftNote>()
            .SetColumns(note => note.IsDeleted == false)
            .Where(note => note.BasicId == id)
            .ExecuteCommand();
    }

    private void Seed(long id, bool isDeleted, long tenantId = 0)
    {
        using var client = CreateRawClient();
        _ = client.Insertable(new SoftNote(id) { TenantId = tenantId, IsDeleted = isDeleted, Text = $"note-{id}" })
            .ExecuteCommand();
    }

    private SoftNote? LoadRaw(long id)
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
    [SugarTable(TableName = "Soft_Note")]
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
