// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.VirtualFileSystem.Services;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 曦寒虚拟文件系统模块测试
/// </summary>
/// <remarks>
/// 模块只做一件事：把服务集合里的配置取出来转交给 AddXiHanVirtualFileSystem。
/// 配置缺失时它必须显式失败而不是静默降级——静默降级会让宿主以为配置生效了，
/// 直到运行期才发现挂载目录和预期不一致。
/// </remarks>
public class XiHanVirtualFileSystemModuleTests
{
    /// <summary>
    /// 配置服务后核心服务与附加服务都已登记
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersVirtualFileSystemServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var context = new ServiceConfigurationContext(services);

        new XiHanVirtualFileSystemModule().ConfigureServices(context);

        Assert.Contains(services, x => x.ServiceType == typeof(IVirtualFileSystem));
        Assert.Contains(services, x => x.ServiceType == typeof(IFileVersioningService));
    }

    /// <summary>
    /// 服务集合里没有配置时抛框架异常
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());

        Assert.Throws<XiHanException>(() => new XiHanVirtualFileSystemModule().ConfigureServices(context));
    }

    /// <summary>
    /// 模块继承自框架模块基类，才能被模块加载器发现
    /// </summary>
    [Fact]
    public void Type_DerivesFromXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanVirtualFileSystemModule());
    }
}
