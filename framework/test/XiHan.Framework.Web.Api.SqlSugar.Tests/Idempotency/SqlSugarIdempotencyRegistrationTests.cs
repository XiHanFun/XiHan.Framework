// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.Web.Api.Idempotency;
using XiHan.Framework.Web.Api.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Web.Api.SqlSugar.Idempotency;

namespace XiHan.Framework.Web.Api.SqlSugar.Tests.Idempotency;

/// <summary>
/// SqlSugar 幂等存储注册测试
/// </summary>
public class SqlSugarIdempotencyRegistrationTests
{
    /// <summary>
    /// 注册后以作用域生命周期的 SqlSugar 存储替换默认存储
    /// </summary>
    [Fact]
    public void 注册后替换默认存储为作用域SqlSugar存储()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIdempotencyStore, DefaultIdempotencyStore>();

        services.AddXiHanWebApiSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IIdempotencyStore));
        Assert.Equal(typeof(SqlSugarIdempotencyStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Contains(services, service => service.ServiceType == typeof(TimeProvider));
    }

    /// <summary>
    /// 幂等键最大长度超过记录列长度时启动校验失败
    /// </summary>
    [Fact]
    public void 幂等键最大长度超过128时启动校验失败()
    {
        var services = new ServiceCollection();
        services.AddOptions<XiHanIdempotencyOptions>().Configure(options => options.MaxKeyLength = 129);
        services.AddXiHanWebApiSqlSugar(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetService<IStartupValidator>();

        Assert.NotNull(validator);
        Assert.Throws<OptionsValidationException>(validator.Validate);
    }

    /// <summary>
    /// 幂等键最大长度为 128 时启动校验通过
    /// </summary>
    [Fact]
    public void 幂等键最大长度为128时启动校验通过()
    {
        var services = new ServiceCollection();
        services.AddOptions<XiHanIdempotencyOptions>().Configure(options => options.MaxKeyLength = 128);
        services.AddXiHanWebApiSqlSugar(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    /// <summary>
    /// 模块同时依赖 Web API 模块与数据模块
    /// </summary>
    [Fact]
    public void 模块依赖WebApi模块与数据模块()
    {
        var dependsOn = typeof(XiHanWebApiSqlSugarModule).GetCustomAttribute<DependsOnAttribute>();

        Assert.NotNull(dependsOn);
        Assert.Contains(typeof(XiHanWebApiModule), dependsOn.DependedTypes);
        Assert.Contains(typeof(XiHanDataModule), dependsOn.DependedTypes);
    }
}
