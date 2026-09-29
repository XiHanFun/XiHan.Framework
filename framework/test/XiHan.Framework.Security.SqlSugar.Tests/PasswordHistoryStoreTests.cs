// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Security.Services;
using XiHan.Framework.Security.SqlSugar.Entities;
using XiHan.Framework.Security.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Security.SqlSugar.Services;

namespace XiHan.Framework.Security.SqlSugar.Tests;

/// <summary>
/// 密码历史存储测试
/// </summary>
public class PasswordHistoryStoreTests
{
    /// <summary>
    /// 写入后能读回
    /// </summary>
    [Fact]
    public async Task 写入后能读回()
    {
        using var context = new PasswordHistoryTestContext();

        await context.Store.RecordPasswordAsync(1L, "hash-1");

        var recent = await context.Store.GetRecentPasswordHashesAsync(1L, 10);

        Assert.Single(recent);
        Assert.Equal("hash-1", recent[0]);
    }

    /// <summary>
    /// 读回顺序为旧到新
    /// </summary>
    [Fact]
    public async Task 读回顺序为旧到新()
    {
        using var context = new PasswordHistoryTestContext();

        await context.Store.RecordPasswordAsync(1L, "hash-1");
        await context.Store.RecordPasswordAsync(1L, "hash-2");
        await context.Store.RecordPasswordAsync(1L, "hash-3");

        var recent = await context.Store.GetRecentPasswordHashesAsync(1L, 10);

        Assert.Equal(["hash-1", "hash-2", "hash-3"], recent);
    }

    /// <summary>
    /// 超过上限时裁剪最旧记录
    /// </summary>
    [Fact]
    public async Task 超过上限时裁剪最旧记录()
    {
        using var context = new PasswordHistoryTestContext();

        await context.Store.RecordPasswordAsync(1L, "hash-1", maxHistoryCount: 2);
        await context.Store.RecordPasswordAsync(1L, "hash-2", maxHistoryCount: 2);
        await context.Store.RecordPasswordAsync(1L, "hash-3", maxHistoryCount: 2);

        var recent = await context.Store.GetRecentPasswordHashesAsync(1L, 10);

        Assert.Equal(["hash-2", "hash-3"], recent);
    }

    /// <summary>
    /// 不同用户的历史互不影响
    /// </summary>
    [Fact]
    public async Task 不同用户的历史互不影响()
    {
        using var context = new PasswordHistoryTestContext();

        await context.Store.RecordPasswordAsync(1L, "user1-hash");
        await context.Store.RecordPasswordAsync(2L, "user2-hash");

        var user1Recent = await context.Store.GetRecentPasswordHashesAsync(1L, 10);

        Assert.Single(user1Recent);
        Assert.Equal("user1-hash", user1Recent[0]);
    }

    /// <summary>
    /// count 非正数时返回空集合
    /// </summary>
    [Fact]
    public async Task count非正数时返回空集合()
    {
        using var context = new PasswordHistoryTestContext();

        await context.Store.RecordPasswordAsync(1L, "hash-1");

        var recent = await context.Store.GetRecentPasswordHashesAsync(1L, 0);

        Assert.Empty(recent);
    }

    /// <summary>
    /// 注册扩展以 SqlSugar 存储顶替内存实现
    /// </summary>
    [Fact]
    public void 注册扩展顶替内存实现()
    {
        var services = new ServiceCollection();
        services.TryAddScoped<IPasswordHistoryStore, DefaultPasswordHistoryStore>();

        services.AddXiHanSecuritySqlSugar();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IPasswordHistoryStore));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 具体存储类型可解析，且与接口解析得到同一实例
    /// </summary>
    [Fact]
    public void 具体类型可注入且与接口同实例()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISqlSugarClientResolver>(new StubClientResolver(new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "DataSource=:memory:",
            DbType = DbType.Sqlite
        })));
        services.AddSingleton(IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload());
        services.TryAddScoped<IPasswordHistoryStore, DefaultPasswordHistoryStore>();

        services.AddXiHanSecuritySqlSugar();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var concrete = scope.ServiceProvider.GetRequiredService<SqlSugarPasswordHistoryStore>();
        var contract = scope.ServiceProvider.GetRequiredService<IPasswordHistoryStore>();

        Assert.Same(concrete, contract);
    }

    /// <summary>
    /// 记录时间相同时裁剪保留主键较大的记录
    /// </summary>
    [Fact]
    public async Task 同一记录时间裁剪保留主键较大者()
    {
        using var context = new PasswordHistoryTestContext();
        var time = DateTimeOffset.UtcNow;

        await context.Client.Insertable(new List<SysPasswordHistory>
        {
            new(1001L) { UserId = 1L, PasswordHash = "small", CreatedTime = time },
            new(1002L) { UserId = 1L, PasswordHash = "large", CreatedTime = time }
        }).ExecuteCommandAsync();

        await context.Store.RecordPasswordAsync(1L, "newest", maxHistoryCount: 2);

        var remaining = await context.Client.Queryable<SysPasswordHistory>()
            .Where(item => item.UserId == 1L)
            .Select(item => item.PasswordHash)
            .ToListAsync();

        Assert.DoesNotContain("small", remaining);
        Assert.Contains("large", remaining);
        Assert.Equal(2, remaining.Count);
    }
}

/// <summary>
/// 密码历史存储测试夹具，提供一个临时 SQLite 库与被测存储实例
/// </summary>
internal sealed class PasswordHistoryTestContext : IDisposable
{
    private readonly string _databaseFile;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PasswordHistoryTestContext()
    {
        _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_password_history_{Guid.NewGuid():N}.db");

        Client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        Client.CodeFirst.InitTables(typeof(SysPasswordHistory));

        Store = new SqlSugarPasswordHistoryStore(
            new StubClientResolver(Client),
            IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload());
    }

    /// <summary>
    /// 被测存储
    /// </summary>
    public SqlSugarPasswordHistoryStore Store { get; }

    /// <summary>
    /// SQLite 客户端
    /// </summary>
    public SqlSugarClient Client { get; }

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
