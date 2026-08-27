// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.ObjectStorage.Constants;
using XiHan.Framework.ObjectStorage.Providers;
using XiHan.Framework.ObjectStorage.Services;

namespace XiHan.Framework.ObjectStorage.Tests;

/// <summary>
/// 对象存储模块测试
/// </summary>
/// <remarks>
/// 模块本身只做一件事：从服务集合里取出配置再转调 AddXiHanObjectStorage。
/// 因此这里只验证两点——装配后核心服务与按配置启用的 Provider 都在，以及缺配置时早失败。
/// 只断言注册结果、不解析实例，避免把 Provider 真实创建出来。
/// </remarks>
public class XiHanObjectStorageModuleTests
{
    /// <summary>
    /// 模块继承自框架的模块基类
    /// </summary>
    [Fact]
    public void Module_DerivesFromXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanObjectStorageModule());
    }

    /// <summary>
    /// 服务集合里存在配置时装配出核心服务与启用的提供程序
    /// </summary>
    [Fact]
    public void ConfigureServices_WithConfigurationInServices_RegistersCoreServicesAndProviders()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:ObjectStorage:DefaultProvider"] = ObjectStorageProviderNames.Local,
                ["XiHan:ObjectStorage:EnabledProviders:0"] = ObjectStorageProviderNames.Local
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        new XiHanObjectStorageModule().ConfigureServices(new ServiceConfigurationContext(services));

        var managerDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IFileStorageProviderManager));
        var routerDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IFileStorageRouter));

        Assert.Equal(typeof(DefaultFileStorageProviderManager), managerDescriptor.ImplementationType);
        Assert.Equal(typeof(DefaultFileStorageRouter), routerDescriptor.ImplementationType);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(LocalFileStorageProvider));
    }

    /// <summary>
    /// 服务集合里没有配置时抛 XiHanException
    /// </summary>
    [Fact]
    public void ConfigureServices_WithoutConfigurationInServices_Throws()
    {
        var services = new ServiceCollection();
        var module = new XiHanObjectStorageModule();
        var context = new ServiceConfigurationContext(services);

        Assert.Throws<XiHanException>(() => module.ConfigureServices(context));
    }

    /// <summary>
    /// 模块装配是幂等的，重复执行不会产生重复的核心服务描述符
    /// </summary>
    [Fact]
    public void ConfigureServices_CalledTwice_DoesNotDuplicateCoreServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:ObjectStorage:DefaultProvider"] = ObjectStorageProviderNames.Local
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanObjectStorageModule();

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(IFileStorageProviderManager)));
        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(IFileStorageRouter)));
        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(LocalFileStorageProvider)));
    }
}
