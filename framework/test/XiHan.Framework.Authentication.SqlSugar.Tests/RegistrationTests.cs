// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Authentication.SqlSugar.ExternalLogins;
using XiHan.Framework.Authentication.SqlSugar.Options;
using XiHan.Framework.Authentication.SqlSugar.RefreshTokens;
using XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;
using XiHan.Framework.Authentication.SqlSugar.Users;
using XiHan.Framework.Authentication.Users;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 服务注册测试
/// </summary>
public class RegistrationTests
{
    /// <summary>
    /// 用户存储被顶替为作用域的 SqlSugar 实现
    /// </summary>
    [Fact]
    public void 用户存储被顶替为作用域的SqlSugar实现()
    {
        var services = new ServiceCollection();
        services.TryAddScoped<IUserStore, DefaultUserStore>();

        services.AddXiHanAuthenticationSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IUserStore));
        Assert.Equal(typeof(SqlSugarUserStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 注册系统时间提供程序
    /// </summary>
    [Fact]
    public void 注册系统时间提供程序()
    {
        var services = new ServiceCollection();

        services.AddXiHanAuthenticationSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TimeProvider));
        Assert.Same(TimeProvider.System, descriptor.ImplementationInstance);
    }

    /// <summary>
    /// 已注册的时间提供程序不被覆盖
    /// </summary>
    [Fact]
    public void 已注册的时间提供程序不被覆盖()
    {
        var services = new ServiceCollection();
        var custom = new MutableTimeProvider(DateTimeOffset.UtcNow);
        services.AddSingleton<TimeProvider>(custom);

        services.AddXiHanAuthenticationSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TimeProvider));
        Assert.Same(custom, descriptor.ImplementationInstance);
    }

    /// <summary>
    /// 空参数抛出
    /// </summary>
    [Fact]
    public void 空参数抛出()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddXiHanAuthenticationSqlSugar(configuration));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddXiHanAuthenticationSqlSugar(null!));
    }

    /// <summary>
    /// 刷新令牌存储被顶替为单例的 SqlSugar 实现
    /// </summary>
    [Fact]
    public void 刷新令牌存储被顶替为单例的SqlSugar实现()
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<IRefreshTokenStore, DefaultRefreshTokenStore>();

        services.AddXiHanAuthenticationSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IRefreshTokenStore));
        Assert.Equal(typeof(SqlSugarRefreshTokenStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 第三方登录存储被顶替为作用域的 SqlSugar 实现
    /// </summary>
    [Fact]
    public void 第三方登录存储被顶替为作用域的SqlSugar实现()
    {
        var services = new ServiceCollection();
        services.TryAddScoped<IExternalLoginStore, DefaultExternalLoginStore>();

        services.AddXiHanAuthenticationSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IExternalLoginStore));
        Assert.Equal(typeof(SqlSugarExternalLoginStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 未启用第三方登录时也注册第三方登录存储
    /// </summary>
    [Fact]
    public void 未启用第三方登录时也注册第三方登录存储()
    {
        var services = new ServiceCollection();

        services.AddXiHanAuthenticationSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IExternalLoginStore));
        Assert.Equal(typeof(SqlSugarExternalLoginStore), descriptor.ImplementationType);
    }

    /// <summary>
    /// 绑定配置节
    /// </summary>
    [Fact]
    public void 绑定配置节()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Authentication:SqlSugar:RefreshTokenReuseDetection"] = "false",
                ["XiHan:Authentication:SqlSugar:RefreshTokenReuseGracePeriod"] = "00:00:30",
                ["XiHan:Authentication:SqlSugar:RefreshTokenCleanupFrequency"] = "16"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddXiHanAuthenticationSqlSugar(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanAuthenticationSqlSugarOptions>>().Value;
        Assert.False(options.RefreshTokenReuseDetection);
        Assert.Equal(TimeSpan.FromSeconds(30), options.RefreshTokenReuseGracePeriod);
        Assert.Equal(16, options.RefreshTokenCleanupFrequency);
    }

    /// <summary>
    /// 校验作用域的容器能解析刷新令牌存储
    /// </summary>
    [Fact]
    public void 校验作用域的容器能解析刷新令牌存储()
    {
        using var context = new AuthenticationTestContext(typeof(SysAuthRefreshToken));
        var services = new ServiceCollection();
        services.AddScoped<ISqlSugarClientResolver>(_ => context.Resolver);
        services.AddScoped<ICurrentTenant>(_ => context.Tenant);
        services.AddSingleton(context.IdGenerator);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddXiHanAuthenticationSqlSugar(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var store = provider.GetRequiredService<IRefreshTokenStore>();
        store.Save("token-1", "1001", DateTime.UtcNow.AddDays(1));

        Assert.True(store.Validate("token-1", "1001"));
    }
}
