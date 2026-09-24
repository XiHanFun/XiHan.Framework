// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Auditing;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.DistributedIds.SnowflakeIds;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 「平台就是 0 号租户」的读写口径测试
/// </summary>
/// <remarks>
/// 跑在真实 SQLite 上，经 <see cref="SqlSugarConnectionConfigurator"/> 挂与生产完全相同的全局过滤器与插入审计 AOP：
/// 断言无租户上下文不再「看全部、写全部」，跨租户读写只能显式清过滤器或切入目标租户。
/// </remarks>
public sealed class PlatformTenantScopeTests : IDisposable
{
    private const string ConfigId = "Main";
    private const long TenantA = 1;
    private const long TenantB = 2;

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"xihan-platform-scope-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly SqlSugarScope _scope;
    private readonly ICurrentTenant _currentTenant;
    private readonly NoteRepository<SharedNote> _sharedNotes;
    private readonly NoteRepository<StrictNote> _strictNotes;

    /// <summary>
    /// 建库、按生产口径装配过滤器与审计 AOP，并在平台、租户 A、租户 B 各播一行
    /// </summary>
    public PlatformTenantScopeTests()
    {
        AsyncLocalCurrentTenantAccessor.Instance.Current = null;

        var serviceCollection = new ServiceCollection();
        _ = serviceCollection.AddSingleton<ICurrentTenantAccessor>(AsyncLocalCurrentTenantAccessor.Instance);
        _ = serviceCollection.AddTransient<ICurrentTenant, CurrentTenant>();
        _services = serviceCollection.BuildServiceProvider();
        _currentTenant = _services.GetRequiredService<ICurrentTenant>();

        var configurator = new SqlSugarConnectionConfigurator(
            Options.Create(new XiHanSqlSugarCoreOptions()),
            AsyncLocalCurrentTenantAccessor.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(),
            new SqlSugarDataExecutingHandler(
                _services.GetRequiredService<IServiceScopeFactory>(),
                IdGeneratorFactory.CreateSnowflakeIdGenerator(new SnowflakeIdOptions())));

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
            client => configurator.Configure(client.GetConnectionScope(ConfigId)));

        var client = _scope.GetConnectionScope(ConfigId);
        client.CodeFirst.InitTables<SharedNote, StrictNote>();

        var resolver = new FixedClientResolver(client);
        _sharedNotes = new NoteRepository<SharedNote>(resolver);
        _strictNotes = new NoteRepository<StrictNote>(resolver);

        Seed<SharedNote>(null, "platform");
        Seed<SharedNote>(TenantA, "a");
        Seed<SharedNote>(TenantB, "b");
        Seed<StrictNote>(null, "platform");
        Seed<StrictNote>(TenantA, "a");
        Seed<StrictNote>(TenantB, "b");
    }

    /// <summary>
    /// 平台上下文读共享实体只看到平台行，不再看到任何租户的数据
    /// </summary>
    [Fact]
    public async Task PlatformContext_SharedEntity_ShouldSeeOnlyPlatformRows()
    {
        var rows = await _sharedNotes.GetAllAsync();

        Assert.Equal(["platform"], rows.Select(row => row.Text));
    }

    /// <summary>
    /// 租户上下文读共享实体看到平台共享行与本租户行
    /// </summary>
    [Fact]
    public async Task TenantContext_SharedEntity_ShouldSeePlatformAndOwnRows()
    {
        using var tenant = _currentTenant.Change(TenantA);

        var rows = await _sharedNotes.GetAllAsync();

        Assert.Equal(["a", "platform"], rows.Select(row => row.Text).Order());
    }

    /// <summary>
    /// 严格隔离实体两侧都只看自己：平台只看平台行，租户只看本租户行
    /// </summary>
    [Fact]
    public async Task StrictEntity_ShouldSeeOnlyOwnScope()
    {
        var platformRows = await _strictNotes.GetAllAsync();
        using var tenant = _currentTenant.Change(TenantA);
        var tenantRows = await _strictNotes.GetAllAsync();

        Assert.Equal(["platform"], platformRows.Select(row => row.Text));
        Assert.Equal(["a"], tenantRows.Select(row => row.Text));
    }

    /// <summary>
    /// 显式切到 0 号租户与平台上下文等价
    /// </summary>
    [Fact]
    public async Task ChangeToZero_ShouldBehaveAsPlatformContext()
    {
        using var platform = _currentTenant.Change(0);

        var sharedRows = await _sharedNotes.GetAllAsync();
        var strictRows = await _strictNotes.GetAllAsync();

        Assert.Equal(["platform"], sharedRows.Select(row => row.Text));
        Assert.Equal(["platform"], strictRows.Select(row => row.Text));
    }

    /// <summary>
    /// 跨租户读取只能显式清过滤器：读共享实体与严格隔离实体都能取到全部租户的行
    /// </summary>
    [Fact]
    public async Task NoTenantQueryable_ShouldSeeAllTenantsForSharedAndStrictEntities()
    {
        var sharedRows = await _sharedNotes.NoTenantQueryable().ToListAsync();
        var strictRows = await _strictNotes.NoTenantQueryable().ToListAsync();

        Assert.Equal(3, sharedRows.Count);
        Assert.Equal(3, strictRows.Count);
    }

    /// <summary>
    /// 平台上下文新增行落 0 号租户
    /// </summary>
    [Fact]
    public async Task PlatformContext_Insert_ShouldStampZero()
    {
        var added = await _sharedNotes.AddAsync(new SharedNote { Text = "new" });

        Assert.Equal(0, LoadIgnoreTenant<SharedNote>(added.BasicId).TenantId);
    }

    /// <summary>
    /// 平台上下文预置租户标识插入被拒：平台不能代写租户数据
    /// </summary>
    [Fact]
    public async Task PlatformContext_InsertWithTenantStamp_ShouldThrow()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sharedNotes.AddAsync(new SharedNote { TenantId = TenantA, Text = "forged" }));

        Assert.Contains("平台上下文只能写入平台数据", exception.Message, StringComparison.Ordinal);
        Assert.Equal(3, CountIgnoreTenant<SharedNote>());
    }

    /// <summary>
    /// 切入租户后新增行落该租户
    /// </summary>
    [Fact]
    public async Task TenantContext_Insert_ShouldStampCurrentTenant()
    {
        SharedNote added;
        using (_currentTenant.Change(TenantB))
        {
            added = await _sharedNotes.AddAsync(new SharedNote { Text = "new" });
        }

        Assert.Equal(TenantB, LoadIgnoreTenant<SharedNote>(added.BasicId).TenantId);
    }

    /// <summary>
    /// 平台上下文改写租户行被拒（行由显式跨租户读取取回，同样写不进去）
    /// </summary>
    [Fact]
    public async Task PlatformContext_UpdateTenantRow_ShouldThrow()
    {
        var row = LoadIgnoreTenant<SharedNote>(TextId("a"));
        row.Text = "changed";

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _sharedNotes.UpdateAsync(row));

        Assert.Equal("a", LoadIgnoreTenant<SharedNote>(row.BasicId).Text);
    }

    /// <summary>
    /// 切入租户后能改写该租户的行
    /// </summary>
    [Fact]
    public async Task TenantContext_UpdateOwnRow_ShouldSucceed()
    {
        var row = LoadIgnoreTenant<SharedNote>(TextId("a"));
        row.Text = "changed";

        using (_currentTenant.Change(TenantA))
        {
            _ = await _sharedNotes.UpdateAsync(row);
        }

        var stored = LoadIgnoreTenant<SharedNote>(row.BasicId);
        Assert.Equal("changed", stored.Text);
        Assert.Equal(TenantA, stored.TenantId);
    }

    /// <summary>
    /// 平台上下文的条件删除只命中平台行
    /// </summary>
    [Fact]
    public async Task PlatformContext_PredicateDelete_ShouldOnlyTouchPlatformRows()
    {
        _ = await _sharedNotes.DeleteAsync(note => note.Text != string.Empty);

        var remaining = _scope.GetConnectionScope(ConfigId)
            .Queryable<SharedNote>()
            .ClearTenantFilter()
            .Select(note => note.Text)
            .ToList();
        Assert.Equal(["a", "b"], remaining.Order());
    }

    /// <summary>
    /// 平台上下文的条件更新只命中平台行
    /// </summary>
    [Fact]
    public async Task PlatformContext_PredicateUpdate_ShouldOnlyTouchPlatformRows()
    {
        _ = await _sharedNotes.UpdateAsync(note => new SharedNote { Text = "touched" }, note => note.Text != string.Empty);

        var texts = _scope.GetConnectionScope(ConfigId)
            .Queryable<SharedNote>()
            .ClearTenantFilter()
            .Select(note => note.Text)
            .ToList();
        Assert.Equal(["a", "b", "touched"], texts.Order());
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        AsyncLocalCurrentTenantAccessor.Instance.Current = null;
        _scope.Dispose();
        _services.Dispose();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private void Seed<TNote>(long? tenantId, string text)
        where TNote : class, INote, new()
    {
        using var scope = _currentTenant.Change(tenantId);
        _ = _scope.GetConnectionScope(ConfigId).Insertable(new TNote { Text = text }).ExecuteCommand();
    }

    private long TextId(string text)
    {
        return _scope.GetConnectionScope(ConfigId)
            .Queryable<SharedNote>()
            .ClearTenantFilter()
            .First(note => note.Text == text)
            .BasicId;
    }

    private TNote LoadIgnoreTenant<TNote>(long id)
        where TNote : class, INote, new()
    {
        return _scope.GetConnectionScope(ConfigId)
            .Queryable<TNote>()
            .ClearTenantFilter()
            .First(note => note.BasicId == id);
    }

    private int CountIgnoreTenant<TNote>()
        where TNote : class, INote, new()
    {
        return _scope.GetConnectionScope(ConfigId)
            .Queryable<TNote>()
            .ClearTenantFilter()
            .Count();
    }

    /// <summary>测试实体共用的形状。</summary>
    public interface INote : IEntityBase<long>, IMultiTenantEntity
    {
        /// <summary>内容。</summary>
        string Text { get; set; }
    }

    /// <summary>读共享多租户实体。</summary>
    [SugarTable(TableName = "Scope_Shared_Note")]
    public class SharedNote : SugarMultiTenantEntity<long>, INote
    {
        /// <summary>内容。</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>严格隔离多租户实体。</summary>
    [SugarTable(TableName = "Scope_Strict_Note")]
    public class StrictNote : SugarMultiTenantEntity<long>, INote, IStrictMultiTenantEntity
    {
        /// <summary>内容。</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>暴露显式跨租户读取入口的仓储。</summary>
    private sealed class NoteRepository<TNote>(ISqlSugarClientResolver clientResolver) : SqlSugarRepositoryBase<TNote, long>(clientResolver)
        where TNote : class, INote, new()
    {
        public ISugarQueryable<TNote> NoTenantQueryable() => CreateNoTenantQueryable();
    }

    /// <summary>固定返回同一客户端的解析器替身。</summary>
    private sealed class FixedClientResolver(ISqlSugarClient client) : ISqlSugarClientResolver
    {
        public ISqlSugarClient GetCurrentClient() => client;

        public ISqlSugarClient GetClientForEntity(Type entityType) => client;

        public ISqlSugarClient GetClient(string configId) => client;

        public IReadOnlyCollection<string> GetAllConfigIds() => [ConfigId];

        public IReadOnlyList<string> GetCurrentLayoutConfigIds() => [ConfigId];

        public IEnumerable<ISqlSugarClient> GetAllClients() => [client];

        public ITenant AsTenant() => throw new NotSupportedException("用例不涉及多库切换。");
    }
}
