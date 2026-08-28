// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.VirtualFileSystem.Extensions.DependencyInjection;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Services;
using XiHan.Framework.VirtualFileSystem.Tests.TestSupport;
using VirtualFileSystemCore = XiHan.Framework.VirtualFileSystem.VirtualFileSystem;

namespace XiHan.Framework.VirtualFileSystem.Tests.Extensions.DependencyInjection;

/// <summary>
/// 虚拟文件系统服务注册扩展测试
/// </summary>
/// <remarks>
/// 注册用的是 TryAddSingleton，所以既要验证「默认注册进得来」，也要验证「已有实现不被覆盖」——
/// 后者是宿主替换实现的唯一入口，回归时最容易被误改成 AddSingleton。
/// 解析核心服务时统一关掉自动挂载与变更追踪，避免把测试宿主输出目录整个扫一遍。
/// </remarks>
public class XiHanVirtualFileSystemServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanVirtualFileSystem_WhenServicesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _ = XiHanVirtualFileSystemServiceCollectionExtensions.AddXiHanVirtualFileSystem(null!));
    }

    /// <summary>
    /// 返回同一个服务集合，支持链式注册
    /// </summary>
    [Fact]
    public void AddXiHanVirtualFileSystem_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanVirtualFileSystem());
    }

    /// <summary>
    /// 核心服务以单例注册，实现类型为框架内实现
    /// </summary>
    [Fact]
    public void AddXiHanVirtualFileSystem_RegistersCoreServicesAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanVirtualFileSystem();

        var fileSystem = Assert.Single(services, x => x.ServiceType == typeof(IVirtualFileSystem));
        Assert.Equal(ServiceLifetime.Singleton, fileSystem.Lifetime);
        Assert.Equal(typeof(VirtualFileSystemCore), fileSystem.ImplementationType);

        var versioning = Assert.Single(services, x => x.ServiceType == typeof(IFileVersioningService));
        Assert.Equal(ServiceLifetime.Singleton, versioning.Lifetime);
        Assert.Equal(typeof(FileVersioningService), versioning.ImplementationType);
    }

    /// <summary>
    /// 重复调用不会产生重复注册
    /// </summary>
    [Fact]
    public void AddXiHanVirtualFileSystem_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();

        services.AddXiHanVirtualFileSystem();
        services.AddXiHanVirtualFileSystem();

        Assert.Single(services, x => x.ServiceType == typeof(IVirtualFileSystem));
        Assert.Single(services, x => x.ServiceType == typeof(IFileVersioningService));
    }

    /// <summary>
    /// 已有实现不会被框架默认实现覆盖
    /// </summary>
    [Fact]
    public void AddXiHanVirtualFileSystem_WhenAlreadyRegistered_KeepsExistingImplementation()
    {
        var services = new ServiceCollection();
        var existing = new FakeVirtualFileSystem();
        services.AddSingleton<IVirtualFileSystem>(existing);

        services.AddXiHanVirtualFileSystem();

        var descriptor = Assert.Single(services, x => x.ServiceType == typeof(IVirtualFileSystem));
        Assert.Same(existing, descriptor.ImplementationInstance);
    }

    /// <summary>
    /// 解析出的虚拟文件系统是可用的单例
    /// </summary>
    [Fact]
    public void Resolve_VirtualFileSystem_IsUsableSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IVirtualFileSystem>();

        Assert.IsType<VirtualFileSystemCore>(first);
        Assert.Same(first, provider.GetRequiredService<IVirtualFileSystem>());
        Assert.False(first.FileExists("/definitely-not-exists.json"));
    }

    /// <summary>
    /// 解析出的版本服务是可用的单例
    /// </summary>
    [Fact]
    public void Resolve_FileVersioningService_IsUsableSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IFileVersioningService>();

        Assert.IsType<FileVersioningService>(first);
        Assert.Same(first, provider.GetRequiredService<IFileVersioningService>());
    }

    /// <summary>
    /// 不传配置时选项保持默认值
    /// </summary>
    [Fact]
    public void AddXiHanVirtualFileSystem_WithoutConfiguration_KeepsDefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddXiHanVirtualFileSystem();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<VirtualFileSystemOptions>>().Value;

        Assert.Equal(500, options.ChangeDebounceMilliseconds);
        Assert.True(options.EnableChangeTracking);
    }

    /// <summary>
    /// 传入配置时按约定的配置节绑定选项
    /// </summary>
    [Fact]
    public void AddXiHanVirtualFileSystem_WithConfiguration_BindsOptionsSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:VirtualFileSystem:ChangeDebounceMilliseconds"] = "1234",
                ["XiHan:VirtualFileSystem:EnableChangeTracking"] = "false",
                ["XiHan:VirtualFileSystem:IncludeCurrentDirectory"] = "false",
                ["XiHan:VirtualFileSystem:IncludeAppBaseDirectory"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddXiHanVirtualFileSystem(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<VirtualFileSystemOptions>>().Value;

        Assert.Equal(1234, options.ChangeDebounceMilliseconds);
        Assert.False(options.EnableChangeTracking);
        Assert.False(options.IncludeCurrentDirectory);
        Assert.False(options.IncludeAppBaseDirectory);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddXiHanVirtualFileSystem();
        services.Configure<VirtualFileSystemOptions>(options =>
        {
            options.IncludeCurrentDirectory = false;
            options.IncludeAppBaseDirectory = false;
            options.EnableChangeTracking = false;
        });

        return services.BuildServiceProvider();
    }
}
