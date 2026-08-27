// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 应用初始化上下文测试
/// </summary>
/// <remarks>
/// 上下文本身只承载服务提供器，用例锁的是「不接受 null」与「原样透传同一个提供器实例」——
/// 模块拿到的必须是应用初始化时那个作用域提供器，任何包装或替换都会破坏模块的作用域语义。
/// </remarks>
public class ApplicationInitializationContextTests
{
    /// <summary>
    /// 服务提供器为空时抛出参数空异常并带上参数名
    /// </summary>
    [Fact]
    public void Constructor_WhenServiceProviderIsNull_ThrowsArgumentNullException()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => new ApplicationInitializationContext(null!));

        Assert.Equal("serviceProvider", thrown.ParamName);
    }

    /// <summary>
    /// 构造后原样持有传入的服务提供器
    /// </summary>
    [Fact]
    public void Constructor_KeepsSameServiceProviderInstance()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Same(provider, context.ServiceProvider);
    }

    /// <summary>
    /// 上下文落在服务提供器访问器契约上，模块可按该接口取用
    /// </summary>
    [Fact]
    public void Context_ImplementsServiceProviderAccessor()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        var accessor = Assert.IsAssignableFrom<IServiceProviderAccessor>(context);
        Assert.Same(provider, accessor.ServiceProvider);
    }

    /// <summary>
    /// 服务提供器属性只读，构造后不可被替换
    /// </summary>
    [Fact]
    public void ServiceProvider_HasNoSetter()
    {
        var property = typeof(ApplicationInitializationContext)
            .GetProperty(nameof(ApplicationInitializationContext.ServiceProvider));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }
}
