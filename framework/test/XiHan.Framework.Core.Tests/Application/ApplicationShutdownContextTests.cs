// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 应用关闭上下文测试
/// </summary>
/// <remarks>
/// 与初始化上下文成对存在，但刻意<b>没有</b>实现 <see cref="IServiceProviderAccessor"/>，
/// 这条差异同样锁进用例：关闭阶段的上下文不参与服务提供器访问器的通用装配。
/// </remarks>
public class ApplicationShutdownContextTests
{
    /// <summary>
    /// 服务提供器为空时抛出参数空异常并带上参数名
    /// </summary>
    [Fact]
    public void Constructor_WhenServiceProviderIsNull_ThrowsArgumentNullException()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => new ApplicationShutdownContext(null!));

        Assert.Equal("serviceProvider", thrown.ParamName);
    }

    /// <summary>
    /// 构造后原样持有传入的服务提供器
    /// </summary>
    [Fact]
    public void Constructor_KeepsSameServiceProviderInstance()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new ApplicationShutdownContext(provider);

        Assert.Same(provider, context.ServiceProvider);
    }

    /// <summary>
    /// 关闭上下文不落在服务提供器访问器契约上
    /// </summary>
    [Fact]
    public void Context_DoesNotImplementServiceProviderAccessor()
    {
        Assert.False(typeof(IServiceProviderAccessor).IsAssignableFrom(typeof(ApplicationShutdownContext)));
    }

    /// <summary>
    /// 服务提供器属性只读，构造后不可被替换
    /// </summary>
    [Fact]
    public void ServiceProvider_HasNoSetter()
    {
        var property = typeof(ApplicationShutdownContext)
            .GetProperty(nameof(ApplicationShutdownContext.ServiceProvider));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }
}
