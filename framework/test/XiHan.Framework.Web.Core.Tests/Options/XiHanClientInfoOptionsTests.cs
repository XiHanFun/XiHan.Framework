// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Core.Options;

namespace XiHan.Framework.Web.Core.Tests.Options;

/// <summary>
/// 客户端信息解析配置测试
/// </summary>
/// <remarks>
/// 配置节名是写进 appsettings 的对外契约，改一个字就会让线上配置整段失效，必须锁死字面量；
/// 两个默认值决定了"没写任何配置时框架的行为"，同样锁住。
/// </remarks>
public class XiHanClientInfoOptionsTests
{
    /// <summary>
    /// 配置节名不得漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:Web:Core:ClientInfo", XiHanClientInfoOptions.SectionName);
    }

    /// <summary>
    /// 默认开启 IP 地理解析，并指向随包发布的相对路径数据库
    /// </summary>
    [Fact]
    public void Defaults_EnableIpRegionAndPointToBundledDatabase()
    {
        var options = new XiHanClientInfoOptions();

        Assert.True(options.EnableIpRegion);
        Assert.Equal("IpDatabases/ip2region.xdb", options.Ip2RegionDbPath);
    }

    /// <summary>
    /// 数据库路径允许显式置空，表示只依赖内置候选路径
    /// </summary>
    [Fact]
    public void Ip2RegionDbPath_AcceptsNull()
    {
        var options = new XiHanClientInfoOptions { Ip2RegionDbPath = null };

        Assert.Null(options.Ip2RegionDbPath);
    }

    /// <summary>
    /// 按约定的配置节名能把两个字段都绑定进来
    /// </summary>
    [Fact]
    public void Bind_FromConfiguredSection_OverridesDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Web:Core:ClientInfo:EnableIpRegion"] = "false",
                ["XiHan:Web:Core:ClientInfo:Ip2RegionDbPath"] = "custom/ip2region.xdb"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<XiHanClientInfoOptions>(configuration.GetSection(XiHanClientInfoOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanClientInfoOptions>>().Value;

        Assert.False(options.EnableIpRegion);
        Assert.Equal("custom/ip2region.xdb", options.Ip2RegionDbPath);
    }

    /// <summary>
    /// 配置里没有这一节时保留默认值，不会被绑定成空
    /// </summary>
    [Fact]
    public void Bind_WhenSectionAbsent_KeepsDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.Configure<XiHanClientInfoOptions>(configuration.GetSection(XiHanClientInfoOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanClientInfoOptions>>().Value;

        Assert.True(options.EnableIpRegion);
        Assert.Equal("IpDatabases/ip2region.xdb", options.Ip2RegionDbPath);
    }

    /// <summary>
    /// 配置节名写错一个层级就绑不上，这里用错误节名反证节名确实是生效路径
    /// </summary>
    [Fact]
    public void Bind_FromWrongSection_DoesNotTakeEffect()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Web:ClientInfo:EnableIpRegion"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<XiHanClientInfoOptions>(configuration.GetSection(XiHanClientInfoOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<IOptions<XiHanClientInfoOptions>>().Value.EnableIpRegion);
    }
}
