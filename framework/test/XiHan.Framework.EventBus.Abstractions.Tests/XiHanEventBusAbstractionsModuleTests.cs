// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.ObjectMapping;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 事件总线抽象模块测试
/// </summary>
/// <remarks>
/// 抽象包本身不注册实现，模块的价值全在依赖声明上：它把对象映射模块拉进模块图，
/// 事件盒记录的扩展属性（<c>ExtraProperties</c>）才有对应的实现支撑。
/// 依赖声明一旦丢失，故障会推迟到运行期才暴露，因此在这里锁死。
/// </remarks>
public class XiHanEventBusAbstractionsModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，才能被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.True(typeof(XiHanEventBusAbstractionsModule).IsAssignableTo(typeof(XiHanModule)));
        Assert.True(typeof(XiHanEventBusAbstractionsModule).IsAssignableTo(typeof(IXiHanModule)));
    }

    /// <summary>
    /// 模块仅依赖对象映射模块
    /// </summary>
    [Fact]
    public void Module_DependsOnObjectMappingModule()
    {
        var attribute = typeof(XiHanEventBusAbstractionsModule)
            .GetCustomAttribute<DependsOnAttribute>(false);

        Assert.NotNull(attribute);

        var dependedTypes = attribute.GetDependedTypes();

        Assert.Equal(typeof(XiHanObjectMappingModule), Assert.Single(dependedTypes));
    }

    /// <summary>
    /// 服务配置在具备配置源时正常完成
    /// </summary>
    [Fact]
    public void ConfigureServices_WithConfiguration_DoesNotThrow()
    {
        var context = CreateContext();
        var module = new XiHanEventBusAbstractionsModule();

        module.ConfigureServices(context);

        Assert.Contains(context.Services, x => x.ServiceType == typeof(IConfiguration));
    }

    /// <summary>
    /// 抽象包不向容器注册任何实现，实现由具体事件总线包提供
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersNoEventBusImplementation()
    {
        var context = CreateContext();
        var module = new XiHanEventBusAbstractionsModule();

        module.ConfigureServices(context);

        Assert.DoesNotContain(context.Services, x => x.ServiceType == typeof(IEventBus));
        Assert.DoesNotContain(context.Services, x => x.ServiceType == typeof(IEventHandlerInvoker));
        Assert.DoesNotContain(context.Services, x => x.ServiceType == typeof(IEventNameProvider));
    }

    /// <summary>
    /// 异步入口与同步入口行为一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_DelegatesToSyncOverload()
    {
        var context = CreateContext();
        var module = new XiHanEventBusAbstractionsModule();

        await module.ConfigureServicesAsync(context);

        Assert.DoesNotContain(context.Services, x => x.ServiceType == typeof(IEventBus));
    }

    /// <summary>
    /// 构造带有配置源的服务配置上下文
    /// </summary>
    /// <returns>服务配置上下文</returns>
    private static ServiceConfigurationContext CreateContext()
    {
        var services = new ServiceCollection();

        // 模块的 ConfigureServices 会读取配置，缺少 IConfiguration 单例实例会直接抛出
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        return new ServiceConfigurationContext(services);
    }
}
