// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Upgrade.Options;

namespace XiHan.Framework.Upgrade.Tests.Options;

/// <summary>
/// 曦寒升级选项测试
/// </summary>
/// <remarks>
/// 升级是「默认不做危险动作」的语义：文件替换与滚动重启默认关闭、维护模式默认开启，
/// 这些默认值一旦漂移会直接改变线上升级行为，因此逐项锁定。
/// </remarks>
public class XiHanUpgradeOptionsTests
{
    /// <summary>
    /// 配置节名称是对外契约，不允许改动
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:Upgrade", XiHanUpgradeOptions.SectionName);
    }

    /// <summary>
    /// 全部默认值符合「保守升级」约定
    /// </summary>
    [Fact]
    public void Defaults_MatchConservativeUpgradeContract()
    {
        var options = new XiHanUpgradeOptions();

        Assert.Equal("0.0.0", options.MinSupportVersion);
        Assert.Null(options.AppVersion);
        Assert.Equal("UpdateScripts", options.MigrationsRootPath);
        Assert.Equal("SystemUpgrade", options.LockResourceKey);
        Assert.Equal(600, options.LockExpirySeconds);
        Assert.True(options.EnableAutoCheckOnStartup);
        Assert.Null(options.NodeName);
        Assert.Null(options.PrimaryNodeName);
        Assert.False(options.EnableMultiTenantIsolation);
        Assert.Null(options.ConnectionConfigId);
        Assert.True(options.EnableMaintenanceMode);
        Assert.False(options.EnableFileUpdate);
        Assert.False(options.EnableRollingRestart);
    }

    /// <summary>
    /// 配置节可以覆盖默认值，键名与属性名一一对应
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_OverridesDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Upgrade:MinSupportVersion"] = "1.2.0",
                ["XiHan:Upgrade:AppVersion"] = "2.3.4",
                ["XiHan:Upgrade:MigrationsRootPath"] = "Scripts",
                ["XiHan:Upgrade:LockResourceKey"] = "MyUpgrade",
                ["XiHan:Upgrade:LockExpirySeconds"] = "30",
                ["XiHan:Upgrade:EnableAutoCheckOnStartup"] = "false",
                ["XiHan:Upgrade:NodeName"] = "node-a",
                ["XiHan:Upgrade:PrimaryNodeName"] = "node-a",
                ["XiHan:Upgrade:EnableMultiTenantIsolation"] = "true",
                ["XiHan:Upgrade:ConnectionConfigId"] = "upgrade-db",
                ["XiHan:Upgrade:EnableMaintenanceMode"] = "false",
                ["XiHan:Upgrade:EnableFileUpdate"] = "true",
                ["XiHan:Upgrade:EnableRollingRestart"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<XiHanUpgradeOptions>(configuration.GetSection(XiHanUpgradeOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanUpgradeOptions>>().Value;

        Assert.Equal("1.2.0", options.MinSupportVersion);
        Assert.Equal("2.3.4", options.AppVersion);
        Assert.Equal("Scripts", options.MigrationsRootPath);
        Assert.Equal("MyUpgrade", options.LockResourceKey);
        Assert.Equal(30, options.LockExpirySeconds);
        Assert.False(options.EnableAutoCheckOnStartup);
        Assert.Equal("node-a", options.NodeName);
        Assert.Equal("node-a", options.PrimaryNodeName);
        Assert.True(options.EnableMultiTenantIsolation);
        Assert.Equal("upgrade-db", options.ConnectionConfigId);
        Assert.False(options.EnableMaintenanceMode);
        Assert.True(options.EnableFileUpdate);
        Assert.True(options.EnableRollingRestart);
    }

    /// <summary>
    /// 配置节缺失时全部回落到默认值，而不是抛异常
    /// </summary>
    [Fact]
    public void Bind_WhenSectionMissing_KeepsDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.Configure<XiHanUpgradeOptions>(configuration.GetSection(XiHanUpgradeOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanUpgradeOptions>>().Value;

        Assert.Equal("0.0.0", options.MinSupportVersion);
        Assert.Equal("UpdateScripts", options.MigrationsRootPath);
        Assert.Equal(600, options.LockExpirySeconds);
        Assert.True(options.EnableMaintenanceMode);
    }
}
