// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.ExternalLogins;
using XiHan.Framework.Authentication.SqlSugar.Options;
using XiHan.Framework.Authentication.SqlSugar.RefreshTokens;
using XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;
using XiHan.Framework.Authentication.SqlSugar.Users;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 认证存储测试夹具，提供一个临时 SQLite 库与存储所需的替身
/// </summary>
internal sealed class AuthenticationTestContext : IDisposable
{
    private readonly string _databaseFile;
    private readonly List<ServiceProvider> _serviceProviders = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="entityTypes">要建表的实体类型，为空时只建用户表</param>
    public AuthenticationTestContext(params Type[] entityTypes)
    {
        _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_auth_{Guid.NewGuid():N}.db");

        Client = new SqlSugarClient(new ConnectionConfig
        {
            // 关闭连接池，用例结束后驱动不再持有临时库文件句柄
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        if (entityTypes.Length == 0)
        {
            Client.CodeFirst.InitTables(typeof(SysAuthUser));
        }
        else
        {
            Client.CodeFirst.InitTables(entityTypes);
        }

        Resolver = new StubClientResolver(Client);
    }

    /// <summary>
    /// 临时库的客户端
    /// </summary>
    public SqlSugarClient Client { get; }

    /// <summary>
    /// 桩解析器
    /// </summary>
    public StubClientResolver Resolver { get; }

    /// <summary>
    /// 当前租户替身
    /// </summary>
    public FakeCurrentTenant Tenant { get; } = new();

    /// <summary>
    /// 可调时钟
    /// </summary>
    public MutableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>
    /// 雪花主键生成器
    /// </summary>
    public IDistributedIdGenerator<long> IdGenerator { get; } = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload();

    /// <summary>
    /// 创建用户存储，每次返回新实例，代表一个新的请求作用域
    /// </summary>
    /// <returns>用户存储</returns>
    public SqlSugarUserStore CreateUserStore()
    {
        return new SqlSugarUserStore(Resolver, Tenant, IdGenerator, Clock);
    }

    /// <summary>
    /// 创建刷新令牌存储
    /// </summary>
    /// <param name="options">存储配置，为空时使用默认配置</param>
    /// <param name="resolver">客户端解析器，为空时使用夹具的桩解析器</param>
    /// <returns>刷新令牌存储</returns>
    public SqlSugarRefreshTokenStore CreateRefreshTokenStore(
        XiHanAuthenticationSqlSugarOptions? options = null,
        ISqlSugarClientResolver? resolver = null)
    {
        return new SqlSugarRefreshTokenStore(
            CreateScopeFactory(resolver ?? Resolver),
            IdGenerator,
            Clock,
            Microsoft.Extensions.Options.Options.Create(options ?? new XiHanAuthenticationSqlSugarOptions()),
            NullLogger<SqlSugarRefreshTokenStore>.Instance);
    }

    /// <summary>
    /// 创建连接同一临时库的新客户端
    /// </summary>
    /// <param name="autoClose">是否每条命令后自动关闭连接</param>
    /// <returns>客户端</returns>
    public SqlSugarClient CreateClient(bool autoClose)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = autoClose
        });
    }

    /// <summary>
    /// 创建第三方登录存储，每次返回新实例
    /// </summary>
    /// <returns>第三方登录存储</returns>
    public SqlSugarExternalLoginStore CreateExternalLoginStore()
    {
        return new SqlSugarExternalLoginStore(Resolver, Tenant, IdGenerator, Clock);
    }

    private IServiceScopeFactory CreateScopeFactory(ISqlSugarClientResolver resolver)
    {
        var provider = new ServiceCollection()
            .AddSingleton(resolver)
            .AddSingleton<ICurrentTenant>(Tenant)
            .BuildServiceProvider();
        _serviceProviders.Add(provider);

        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>
    /// 释放客户端并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        foreach (var serviceProvider in _serviceProviders)
        {
            serviceProvider.Dispose();
        }

        Client.Dispose();

        string[] files = [_databaseFile, $"{_databaseFile}-wal", $"{_databaseFile}-shm"];
        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
