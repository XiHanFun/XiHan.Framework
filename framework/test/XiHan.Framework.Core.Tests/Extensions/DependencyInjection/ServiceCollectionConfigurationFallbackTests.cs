// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务集合读配置的非根配置回落测试
/// </summary>
/// <remarks>
/// <c>GetConfigurationOrNull</c> 的返回类型是 <see cref="IConfiguration"/>，从来没有"必须是配置根"这个要求。
/// 但它曾经写成「主机上下文带了配置 → 强转 <see cref="IConfigurationRoot"/>」，
/// 一旦宿主放进来的是配置节或自定义实现，强转得到 null，方法直接返回 null，
/// 连已登记的 <see cref="IConfiguration"/> 单例都不再回落，<c>GetConfiguration()</c> 随之抛框架异常。
/// <para>
/// 通用主机总是放 <c>ConfigurationRoot</c>，所以这条路径平时看不出来；
/// 这里用配置节把"非根配置"这个前提造出来，直接锁住两级回落的语义。
/// </para>
/// </remarks>
public class ServiceCollectionConfigurationFallbackTests
{
    /// <summary>
    /// 主机上下文里放的是配置节（非配置根）时照样被读回，而不是退化成空
    /// </summary>
    [Fact]
    public void GetConfigurationOrNull_WhenHostContextConfigurationIsNotRoot_ReturnsItAnyway()
    {
        IServiceCollection services = new ServiceCollection();
        var section = BuildSection("来自主机上下文");

        // 前提自检：配置节不是配置根，否则这条用例就没在测它想测的东西
        Assert.False(section is IConfigurationRoot, "配置节不应同时是配置根，否则用例前提不成立");

        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = section
        });

        Assert.Same(section, services.GetConfigurationOrNull());
    }

    /// <summary>
    /// 主机上下文里放的是配置节时，强制读取不再抛异常
    /// </summary>
    [Fact]
    public void GetConfiguration_WhenHostContextConfigurationIsNotRoot_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();
        var section = BuildSection("来自主机上下文");

        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = section
        });

        Assert.Same(section, services.GetConfiguration());
    }

    /// <summary>
    /// 主机上下文里放的是配置节、同时另有直接登记的配置时，仍然是主机上下文优先
    /// </summary>
    /// <remarks>
    /// 修复前这里会返回直接登记的那份吗？不会——它连回落都走不到，直接返回 null。
    /// 修好之后优先级契约必须保持不变：主机上下文第一，直接登记的单例第二。
    /// </remarks>
    [Fact]
    public void GetConfigurationOrNull_WhenHostContextIsNotRoot_StillWinsOverRegisteredInstance()
    {
        IServiceCollection services = new ServiceCollection();
        var section = BuildSection("来自主机上下文");
        var registeredDirectly = BuildRoot("直接登记");

        services.AddSingleton<IConfiguration>(registeredDirectly);
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = section
        });

        Assert.Same(section, services.GetConfigurationOrNull());
    }

    /// <summary>
    /// 读回的配置节仍然可用：能按节内相对键取到值
    /// </summary>
    [Fact]
    public void GetConfigurationOrNull_ReturnedSectionKeepsWorking()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = BuildSection("来自主机上下文")
        });

        var configuration = services.GetConfigurationOrNull();

        Assert.NotNull(configuration);
        Assert.Equal("来自主机上下文", configuration!["Marker"]);
    }

    /// <summary>
    /// 反例：主机上下文与直接登记都没有时仍然返回空，回落链不能凭空造出配置
    /// </summary>
    [Fact]
    public void GetConfigurationOrNull_WhenHostContextCarriesNothing_StaysNull()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>()));

        Assert.Null(services.GetConfigurationOrNull());
    }

    /// <summary>
    /// 构造一份只带样例键的内存配置根
    /// </summary>
    /// <param name="marker">样例值</param>
    /// <returns>配置根</returns>
    private static IConfigurationRoot BuildRoot(string marker)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sample:Marker"] = marker
            })
            .Build();
    }

    /// <summary>
    /// 构造一份非配置根的配置（配置节）
    /// </summary>
    /// <param name="marker">样例值</param>
    /// <returns>配置节</returns>
    private static IConfiguration BuildSection(string marker)
    {
        return BuildRoot(marker).GetSection("Sample");
    }
}
