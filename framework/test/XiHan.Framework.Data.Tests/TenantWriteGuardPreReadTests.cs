// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 写边界豁免作用域内仓储写路径预读的租户过滤测试
/// </summary>
/// <remarks>
/// 跑在真实 SQLite 上、挂与生产同口径的租户读共享过滤器与自动写过滤设置：
/// 断言的是「豁免作用域内能改写带别的租户戳的自有行、作用域外仍被租户边界拦住、租户戳不被改写」。
/// </remarks>
public sealed class TenantWriteGuardPreReadTests : IDisposable
{
    private const string ConfigId = "Main";
    private const long HomeTenantId = 1;
    private const long ActiveTenantId = 2;

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"xihan-write-guard-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _scope;
    private readonly SqlSugarRepositoryBase<TenantNote, long> _repository;

    /// <summary>
    /// 建库、挂租户读共享过滤器、播一行归属租户 1 的数据
    /// </summary>
    public TenantWriteGuardPreReadTests()
    {
        _scope = new SqlSugarScope(
            new ConnectionConfig
            {
                ConfigId = ConfigId,
                ConnectionString = $"DataSource={_databasePath};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                MoreSettings = new ConnMoreSettings
                {
                    IsAutoUpdateQueryFilter = true,
                    IsAutoDeleteQueryFilter = true
                }
            },
            client => client.QueryFilter.AddTableFilter<IMultiTenantEntity>(
                entity => entity.TenantId == 0 ||
                          entity.TenantId == ResolveTenantScopeId()));

        var client = _scope.GetConnectionScope(ConfigId);
        client.CodeFirst.InitTables<TenantNote>();
        _ = client.Insertable(new TenantNote(1) { TenantId = HomeTenantId, Text = "home" }).ExecuteCommand();

        _repository = new SqlSugarRepositoryBase<TenantNote, long>(new FixedClientResolver(client));
    }

    /// <summary>
    /// 作用域外：租户 2 上下文改写租户 1 的行，预读带租户过滤判为不存在
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ForeignTenantRow_WithoutSuppress_ShouldThrow()
    {
        using var tenantScope = EnterTenant(ActiveTenantId);
        var row = LoadIgnoreTenant(1);
        row.Text = "changed";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateAsync(row));

        Assert.Contains("不在当前租户范围内", exception.Message, StringComparison.Ordinal);
        Assert.Equal("home", LoadIgnoreTenant(1).Text);
    }

    /// <summary>
    /// 作用域内：租户 2 上下文改写租户 1 的行成功，且租户戳保持为 1
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ForeignTenantRow_WithinSuppress_ShouldUpdateAndKeepTenantStamp()
    {
        using var tenantScope = EnterTenant(ActiveTenantId);
        var row = LoadIgnoreTenant(1);
        row.Text = "changed";

        using (TenantWriteGuard.Suppress())
        {
            _ = await _repository.UpdateAsync(row);
        }

        var stored = LoadIgnoreTenant(1);
        Assert.Equal("changed", stored.Text);
        Assert.Equal(HomeTenantId, stored.TenantId);
    }

    /// <summary>
    /// 作用域内的批量更新同样能命中带别的租户戳的行
    /// </summary>
    [Fact]
    public async Task UpdateRangeAsync_ForeignTenantRows_WithinSuppress_ShouldUpdate()
    {
        _ = _scope.GetConnectionScope(ConfigId)
            .Insertable(new TenantNote(2) { TenantId = HomeTenantId, Text = "home-2" })
            .ExecuteCommand();

        using var tenantScope = EnterTenant(ActiveTenantId);
        var rows = new[] { LoadIgnoreTenant(1), LoadIgnoreTenant(2) };
        foreach (var row in rows)
        {
            row.Text = "batch";
        }

        using (TenantWriteGuard.Suppress())
        {
            _ = await _repository.UpdateRangeAsync(rows);
        }

        Assert.Equal("batch", LoadIgnoreTenant(1).Text);
        Assert.Equal("batch", LoadIgnoreTenant(2).Text);
    }

    /// <summary>
    /// 作用域结束后租户边界立即恢复
    /// </summary>
    [Fact]
    public async Task UpdateAsync_AfterSuppressScopeDisposed_ShouldThrowAgain()
    {
        using var tenantScope = EnterTenant(ActiveTenantId);
        var row = LoadIgnoreTenant(1);

        using (TenantWriteGuard.Suppress())
        {
            row.Text = "first";
            _ = await _repository.UpdateAsync(row);
        }

        row = LoadIgnoreTenant(1);
        row.Text = "second";
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateAsync(row));
        Assert.Equal("first", LoadIgnoreTenant(1).Text);
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

    private static TenantScope EnterTenant(long tenantId)
    {
        return new TenantScope(tenantId);
    }

    private TenantNote LoadIgnoreTenant(long id)
    {
        return _scope.GetConnectionScope(ConfigId)
            .Queryable<TenantNote>()
            .ClearFilter<IMultiTenantEntity>()
            .First(note => note.BasicId == id);
    }

    /// <summary>测试用多租户实体。</summary>
    [SugarTable(TableName = "Tenant_Note")]
    public class TenantNote : SugarMultiTenantEntity<long>
    {
        /// <summary>构造函数。</summary>
        public TenantNote()
        {
        }

        /// <summary>构造函数。</summary>
        /// <param name="basicId">主键</param>
        public TenantNote(long basicId) : base(basicId)
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
