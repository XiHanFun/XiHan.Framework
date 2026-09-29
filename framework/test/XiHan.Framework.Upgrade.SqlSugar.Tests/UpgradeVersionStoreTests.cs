// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Services;
using XiHan.Framework.Upgrade.SqlSugar.Entities;
using XiHan.Framework.Upgrade.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Upgrade.SqlSugar.Services;

namespace XiHan.Framework.Upgrade.SqlSugar.Tests;

/// <summary>
/// 升级版本存储测试
/// </summary>
public class UpgradeVersionStoreTests
{
    /// <summary>
    /// 建表后能创建首行
    /// </summary>
    [Fact]
    public async Task 建表后能创建首行()
    {
        using var context = new UpgradeStoreTestContext();

        await context.Store.EnsureTablesAsync();
        var state = await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");

        Assert.True(state.Id > 0);
        Assert.Equal("1.0.0", state.AppVersion);
        Assert.Equal("0.0.0", state.DbVersion);
        Assert.Equal("0.9.0", state.MinSupportVersion);
    }

    /// <summary>
    /// 二次调用返回同一行
    /// </summary>
    [Fact]
    public async Task 二次调用返回同一行()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        var first = await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");
        var second = await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");

        Assert.Equal(first.Id, second.Id);
    }

    /// <summary>
    /// 回填空白字段
    /// </summary>
    [Fact]
    public async Task 回填空白字段()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        context.Client.Insertable(new SysUpgradeVersion(999L)
        {
            TenantKey = "host",
            AppVersion = "",
            DbVersion = "0.0.0",
            MinSupportVersion = null,
            IsUpgrading = false
        }).ExecuteCommand();

        var state = await context.Store.GetOrCreateAsync("2.0.0", "1.0.0");

        Assert.Equal(999L, state.Id);
        Assert.Equal("2.0.0", state.AppVersion);
        Assert.Equal("1.0.0", state.MinSupportVersion);
    }

    /// <summary>
    /// 插入竞态时退回重查而不是抛异常或产生第二行
    /// </summary>
    /// <remarks>
    /// 用 <see cref="RacingUpgradeVersionStore"/> 在"确认不存在"与"执行插入"之间插入一行竞争数据，
    /// 使被测实例自己的插入因 <c>Tenant_Key</c> 唯一索引冲突而失败，触发重查分支。
    /// </remarks>
    [Fact]
    public async Task 插入竞态时退回重查而不是抛异常或产生第二行()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        var racingStore = new RacingUpgradeVersionStore(
            new StubClientResolver(context.Client),
            IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
            context.Client);

        var state = await racingStore.GetOrCreateAsync("1.0.0", "0.9.0");
        var count = context.Client.Queryable<SysUpgradeVersion>().Count(item => item.TenantKey == "host");

        Assert.Equal("competitor-app-version", state.AppVersion);
        Assert.Equal(1, count);
    }

    /// <summary>
    /// 没有版本记录时按给定版本登记一条并返回 true
    /// </summary>
    [Fact]
    public async Task 登记基线时没有记录则新建并返回true()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        var created = await context.Store.TryCreateBaselineAsync("2.0.0", "1.5.0", "1.0.0");
        var row = context.Client.Queryable<SysUpgradeVersion>().Single(item => item.TenantKey == "host");

        Assert.True(created);
        Assert.Equal("2.0.0", row.AppVersion);
        Assert.Equal("1.5.0", row.DbVersion);
        Assert.Equal("1.0.0", row.MinSupportVersion);
        Assert.False(row.IsUpgrading);
    }

    /// <summary>
    /// 已有版本记录时返回 false 且不改动既有记录
    /// </summary>
    [Fact]
    public async Task 登记基线时已有记录则返回false且不改动()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");

        var created = await context.Store.TryCreateBaselineAsync("2.0.0", "1.5.0", "1.0.0");
        var row = context.Client.Queryable<SysUpgradeVersion>().Single(item => item.TenantKey == "host");

        Assert.False(created);
        Assert.Equal("1.0.0", row.AppVersion);
        Assert.Equal("0.0.0", row.DbVersion);
    }

    /// <summary>
    /// 登记基线时与并发插入冲突，返回 false 且只有一行
    /// </summary>
    [Fact]
    public async Task 登记基线时插入竞态返回false且只有一行()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        var racingStore = new RacingUpgradeVersionStore(
            new StubClientResolver(context.Client),
            IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
            context.Client);

        var created = await racingStore.TryCreateBaselineAsync("2.0.0", "1.5.0", "1.0.0");
        var rows = context.Client.Queryable<SysUpgradeVersion>().Where(item => item.TenantKey == "host").ToList();

        Assert.False(created);
        var row = Assert.Single(rows);
        Assert.Equal("competitor-app-version", row.AppVersion);
    }

    /// <summary>
    /// 创建版本行时插入失败后重查也失败，抛出同时包含插入异常与重查异常的聚合异常
    /// </summary>
    [Fact]
    public async Task 创建版本行时插入失败后重查也失败则抛出包含两个异常的聚合异常()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        var insertFailed = false;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                insertFailed = true;
                throw new InvalidOperationException("insert-boom");
            }

            if (insertFailed && sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("requery-boom");
            }
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => context.Store.GetOrCreateAsync("1.0.0", "0.9.0"));

        var messages = Flatten(exception).Select(item => item.Message).ToList();
        Assert.Contains(messages, message => message.Contains("insert-boom", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("requery-boom", StringComparison.Ordinal));
    }

    /// <summary>
    /// 登记基线时插入失败后重查也失败，抛出同时包含插入异常与重查异常的聚合异常
    /// </summary>
    [Fact]
    public async Task 登记基线时插入失败后重查也失败则抛出包含两个异常的聚合异常()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        var insertFailed = false;
        context.Client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                insertFailed = true;
                throw new InvalidOperationException("insert-boom");
            }

            if (insertFailed && sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("requery-boom");
            }
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => context.Store.TryCreateBaselineAsync("2.0.0", "1.5.0", "1.0.0"));

        var messages = Flatten(exception).Select(item => item.Message).ToList();
        Assert.Contains(messages, message => message.Contains("insert-boom", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("requery-boom", StringComparison.Ordinal));
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
            {
                yield return inner;
            }
        }
        else if (exception.InnerException is not null)
        {
            foreach (var inner in Flatten(exception.InnerException))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// 只有成功记录视为已执行
    /// </summary>
    [Fact]
    public async Task 只有成功记录视为已执行()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        await context.Store.AddMigrationHistoryAsync(new UpgradeMigrationHistory
        {
            Version = "1.0.0",
            ScriptName = "0001.sql",
            ExecutedTime = DateTimeOffset.UtcNow,
            Success = false
        });

        Assert.False(await context.Store.HasMigrationHistoryAsync("1.0.0", "0001.sql"));

        await context.Store.AddMigrationHistoryAsync(new UpgradeMigrationHistory
        {
            Version = "1.0.0",
            ScriptName = "0001.sql",
            ExecutedTime = DateTimeOffset.UtcNow,
            Success = true
        });

        Assert.True(await context.Store.HasMigrationHistoryAsync("1.0.0", "0001.sql"));
    }

    /// <summary>
    /// 版本带前后空白时仍能查到已执行记录
    /// </summary>
    [Fact]
    public async Task 版本带前后空白时仍能查到已执行记录()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        await context.Store.AddMigrationHistoryAsync(new UpgradeMigrationHistory
        {
            Version = "1.0.0",
            ScriptName = "0001.sql",
            ExecutedTime = DateTimeOffset.UtcNow,
            Success = true
        });

        Assert.True(await context.Store.HasMigrationHistoryAsync("  1.0.0 ", "0001.sql"));
    }

    /// <summary>
    /// 返回最新的迁移历史
    /// </summary>
    [Fact]
    public async Task 返回最新的迁移历史()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        await context.Store.AddMigrationHistoryAsync(new UpgradeMigrationHistory
        {
            Version = "1.0.0",
            ScriptName = "0001.sql",
            ExecutedTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            Success = true
        });
        await context.Store.AddMigrationHistoryAsync(new UpgradeMigrationHistory
        {
            Version = "1.1.0",
            ScriptName = "0002.sql",
            ExecutedTime = DateTimeOffset.UtcNow,
            Success = true
        });

        var latest = await context.Store.GetLatestHistoryAsync();

        Assert.NotNull(latest);
        Assert.Equal("0002.sql", latest!.ScriptName);
    }

    /// <summary>
    /// 空表返回 null
    /// </summary>
    [Fact]
    public async Task 空表返回null()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();

        var latest = await context.Store.GetLatestHistoryAsync();

        Assert.Null(latest);
    }

    /// <summary>
    /// 设置升级中状态后回写调用方对象且数据库同步更新
    /// </summary>
    [Fact]
    public async Task 设置升级中状态后回写调用方对象且数据库同步更新()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        var version = await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");
        var startTime = DateTimeOffset.UtcNow;

        await context.Store.SetUpgradingAsync(version, "node-1", startTime);

        Assert.True(version.IsUpgrading);
        Assert.Equal("node-1", version.UpgradeNode);
        Assert.Equal(startTime, version.UpgradeStartTime);

        var reloaded = context.Client.Queryable<SysUpgradeVersion>().Single(item => item.BasicId == version.Id);
        Assert.True(reloaded.IsUpgrading);
        Assert.Equal("node-1", reloaded.UpgradeNode);
    }

    /// <summary>
    /// 设置升级完成状态后回写调用方对象
    /// </summary>
    [Fact]
    public async Task 设置升级完成状态后回写调用方对象()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        var version = await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");
        await context.Store.SetUpgradingAsync(version, "node-1", DateTimeOffset.UtcNow);

        await context.Store.SetUpgradeCompletedAsync(version, "1.1.0", "1.1.0");

        Assert.False(version.IsUpgrading);
        Assert.Equal("1.1.0", version.AppVersion);
        Assert.Equal("1.1.0", version.DbVersion);

        var reloaded = context.Client.Queryable<SysUpgradeVersion>().Single(item => item.BasicId == version.Id);
        Assert.False(reloaded.IsUpgrading);
        Assert.Equal("1.1.0", reloaded.AppVersion);
    }

    /// <summary>
    /// 设置升级失败状态后回写调用方对象
    /// </summary>
    [Fact]
    public async Task 设置升级失败状态后回写调用方对象()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        var version = await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");
        await context.Store.SetUpgradingAsync(version, "node-1", DateTimeOffset.UtcNow);

        await context.Store.SetUpgradeFailedAsync(version);

        Assert.False(version.IsUpgrading);

        var reloaded = context.Client.Queryable<SysUpgradeVersion>().Single(item => item.BasicId == version.Id);
        Assert.False(reloaded.IsUpgrading);
    }

    /// <summary>
    /// 更新数据库版本后回写调用方对象
    /// </summary>
    [Fact]
    public async Task 更新数据库版本后回写调用方对象()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        var version = await context.Store.GetOrCreateAsync("1.0.0", "0.9.0");

        await context.Store.UpdateDbVersionAsync(version, "1.2.0");

        Assert.Equal("1.2.0", version.DbVersion);

        var reloaded = context.Client.Queryable<SysUpgradeVersion>().Single(item => item.BasicId == version.Id);
        Assert.Equal("1.2.0", reloaded.DbVersion);
    }

    /// <summary>
    /// 未经 GetOrCreateAsync 的 version 拒绝写入
    /// </summary>
    [Fact]
    public async Task 未经GetOrCreateAsync的version拒绝写入()
    {
        using var context = new UpgradeStoreTestContext();
        await context.Store.EnsureTablesAsync();
        var version = new UpgradeVersionState { Id = 0 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.Store.UpdateDbVersionAsync(version, "1.0.0"));
    }

    /// <summary>
    /// 注册扩展以 SqlSugar 存储顶替内存实现
    /// </summary>
    [Fact]
    public void 注册扩展顶替内存实现()
    {
        var services = new ServiceCollection();
        services.TryAddScoped<IUpgradeVersionStore, DefaultUpgradeVersionStore>();

        services.AddXiHanUpgradeSqlSugar();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IUpgradeVersionStore));
        Assert.Equal(typeof(SqlSugarUpgradeVersionStore), descriptor.ImplementationType);
    }
}

/// <summary>
/// 升级版本存储测试夹具
/// </summary>
internal sealed class UpgradeStoreTestContext : IDisposable
{
    private readonly string _databaseFile;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UpgradeStoreTestContext()
    {
        _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_upgrade_{Guid.NewGuid():N}.db");

        Client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        Store = new SqlSugarUpgradeVersionStore(
            new StubClientResolver(Client),
            IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload());
    }

    /// <summary>
    /// SQLite 客户端
    /// </summary>
    public SqlSugarClient Client { get; }

    /// <summary>
    /// 被测存储
    /// </summary>
    public SqlSugarUpgradeVersionStore Store { get; }

    /// <summary>
    /// 释放客户端并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        Client.Dispose();

        if (File.Exists(_databaseFile))
        {
            File.Delete(_databaseFile);
        }
    }
}

/// <summary>
/// 测试用子类，在插入前抢先写入一行同租户键的记录，确定性地模拟并发建行
/// </summary>
/// <remarks>
/// 借 <see cref="SqlSugarUpgradeVersionStore.OnBeforeInsertAsync"/> 钩子在"确认不存在"与"执行插入"之间插队，
/// 让基类自己的插入撞上 <c>Tenant_Key</c> 唯一索引而失败。
/// </remarks>
internal sealed class RacingUpgradeVersionStore : SqlSugarUpgradeVersionStore
{
    private readonly SqlSugarClient _client;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    /// <param name="client">用于抢先插入竞争行的客户端</param>
    public RacingUpgradeVersionStore(
        XiHan.Framework.Data.SqlSugar.Clients.ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator,
        SqlSugarClient client)
        : base(clientResolver, idGenerator)
    {
        _client = client;
    }

    /// <summary>
    /// 在基类确认租户键不存在后、正式插入前，抢先写入一行竞争数据
    /// </summary>
    /// <param name="tenantKey">即将插入的租户键</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected override Task OnBeforeInsertAsync(string tenantKey, CancellationToken cancellationToken)
    {
        _client.Insertable(new SysUpgradeVersion(555L)
        {
            TenantKey = tenantKey,
            AppVersion = "competitor-app-version",
            DbVersion = "0.0.0",
            MinSupportVersion = "0.9.0",
            IsUpgrading = false
        }).ExecuteCommand();

        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试用客户端解析器，固定返回同一个客户端
/// </summary>
internal sealed class StubClientResolver : XiHan.Framework.Data.SqlSugar.Clients.ISqlSugarClientResolver
{
    private readonly ISqlSugarClient _client;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">固定返回的客户端</param>
    public StubClientResolver(ISqlSugarClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 获取当前客户端
    /// </summary>
    public ISqlSugarClient GetCurrentClient()
    {
        return _client;
    }

    /// <summary>
    /// 获取实体对应的客户端
    /// </summary>
    /// <param name="entityType">实体类型</param>
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        return _client;
    }

    /// <summary>
    /// 按连接配置标识获取客户端
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    public ISqlSugarClient GetClient(string configId)
    {
        return _client;
    }

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取当前布局的全部连接配置标识
    /// </summary>
    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取当前工作单元已登记的连接配置标识
    /// </summary>
    public IReadOnlyList<string> GetEnlistedConfigIds()
    {
        return [];
    }

    /// <summary>
    /// 获取所有库的客户端
    /// </summary>
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return [_client];
    }

    /// <summary>
    /// 获取底层多租户接口
    /// </summary>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public ITenant AsTenant()
    {
        throw new NotSupportedException("测试桩不支持多租户切换。");
    }
}
