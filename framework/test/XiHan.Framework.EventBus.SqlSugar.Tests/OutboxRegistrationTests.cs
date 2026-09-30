// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.EventBus.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.SqlSugar.Outbox;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 发件箱注册测试
/// </summary>
public class OutboxRegistrationTests
{
    /// <summary>
    /// 入箱连接范围以单例注册
    /// </summary>
    [Fact]
    public void 入箱连接范围以单例注册()
    {
        var services = new ServiceCollection();

        services.AddXiHanSqlSugarEventBus(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(ISqlSugarOutboxConnectionScope));

        Assert.Equal(typeof(AsyncLocalSqlSugarOutboxConnectionScope), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 发件箱实现类型被顶替
    /// </summary>
    [Fact]
    public void 发件箱实现类型被顶替()
    {
        var services = BuildServicesWithDefaults();

        services.AddXiHanSqlSugarEventBus(new ConfigurationBuilder().Build());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XiHanDistributedEventBusOptions>>().Value;

        Assert.Equal(typeof(SqlSugarEventOutbox), options.Outboxes["Default"].ImplementationType);
    }

    /// <summary>
    /// 发件箱接口注册被顶替
    /// </summary>
    [Fact]
    public void 发件箱接口注册被顶替()
    {
        var services = BuildServicesWithDefaults();

        services.AddXiHanSqlSugarEventBus(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IEventOutbox));

        Assert.Equal(typeof(SqlSugarEventOutbox), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 领取超时不为正时读取选项抛出校验异常
    /// </summary>
    [Fact]
    public void 领取超时不为正时读取选项抛出校验异常()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:EventBus:SqlSugar:ClaimTimeout"] = "00:00:00"
            })
            .Build();

        services.AddXiHanSqlSugarEventBus(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<XiHanSqlSugarEventBoxOptions>>();

        Assert.Throws<OptionsValidationException>(() => options.Value);
    }

    /// <summary>
    /// 收件箱保留期不为正时读取选项抛出校验异常
    /// </summary>
    [Fact]
    public void 收件箱保留期不为正时读取选项抛出校验异常()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:EventBus:SqlSugar:InboxRetentionPeriod"] = "-00:00:01"
            })
            .Build();

        services.AddXiHanSqlSugarEventBus(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<XiHanSqlSugarEventBoxOptions>>();

        Assert.Throws<OptionsValidationException>(() => options.Value);
    }

    /// <summary>
    /// 默认配置通过校验
    /// </summary>
    [Fact]
    public void 默认配置通过校验()
    {
        var services = new ServiceCollection();

        services.AddXiHanSqlSugarEventBus(new ConfigurationBuilder().Build());

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<XiHanSqlSugarEventBoxOptions>>();

        Assert.True(options.Value.ClaimTimeout > TimeSpan.Zero);
    }

    /// <summary>
    /// 模拟事件总线模块已注册的默认值
    /// </summary>
    private static ServiceCollection BuildServicesWithDefaults()
    {
        var services = new ServiceCollection();

        services.Configure<XiHanDistributedEventBusOptions>(options =>
        {
            options.Outboxes.Configure(config =>
            {
                if (config.ImplementationType == default)
                {
                    config.ImplementationType = typeof(DefaultEventOutbox);
                }
            });
        });

        services.TryAddSingleton<DefaultEventOutbox>();
        services.TryAddSingleton<IEventOutbox>(sp => sp.GetRequiredService<DefaultEventOutbox>());

        return services;
    }
}
