// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 属性字段自动装配处理器测试
/// </summary>
/// <remarks>
/// 处理器把「带标记的属性和字段」编译成一段赋值委托并按类型缓存，
/// 因此必须验证：只装配带标记的成员、字段与属性都覆盖、同一类型重复装配走缓存后行为不变、
/// 以及目标服务未注册时写入空值而不是抛出。
/// </remarks>
public class AutowiredServiceHandlerTests
{
    /// <summary>
    /// 带标记的属性被装配
    /// </summary>
    [Fact]
    public void Autowired_WhenPropertyMarked_InjectsService()
    {
        using var provider = BuildProvider();
        var handler = new AutowiredServiceHandler(provider);
        var target = new AwsTarget();

        handler.Autowired(target);

        Assert.Same(provider.GetRequiredService<IAwsDependency>(), target.MarkedProperty);
    }

    /// <summary>
    /// 带标记的字段被装配
    /// </summary>
    [Fact]
    public void Autowired_WhenFieldMarked_InjectsService()
    {
        using var provider = BuildProvider();
        var handler = new AutowiredServiceHandler(provider);
        var target = new AwsTarget();

        handler.Autowired(target);

        Assert.Same(provider.GetRequiredService<IAwsDependency>(), target.MarkedField);
    }

    /// <summary>
    /// 未标记的成员保持原样
    /// </summary>
    [Fact]
    public void Autowired_WhenMemberNotMarked_LeavesItUntouched()
    {
        using var provider = BuildProvider();
        var handler = new AutowiredServiceHandler(provider);
        var target = new AwsTarget();

        handler.Autowired(target);

        Assert.Null(target.PlainProperty);
        Assert.Null(target.PlainField);
    }

    /// <summary>
    /// 同一类型重复装配时缓存的委托行为一致
    /// </summary>
    [Fact]
    public void Autowired_WhenSameTypeTwice_UsesCachedActionWithSameResult()
    {
        using var provider = BuildProvider();
        var handler = new AutowiredServiceHandler(provider);
        var first = new AwsTarget();
        var second = new AwsTarget();

        handler.Autowired(first);
        handler.Autowired(second);

        Assert.NotNull(second.MarkedProperty);
        Assert.Same(first.MarkedProperty, second.MarkedProperty);
    }

    /// <summary>
    /// 目标服务未注册时写入空值
    /// </summary>
    [Fact]
    public void Autowired_WhenServiceMissing_AssignsNull()
    {
        IServiceCollection services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var handler = new AutowiredServiceHandler(provider);
        var target = new AwsTarget();

        handler.Autowired(target);

        Assert.Null(target.MarkedProperty);
        Assert.Null(target.MarkedField);
    }

    /// <summary>
    /// 没有任何标记成员的对象装配后不抛出
    /// </summary>
    [Fact]
    public void Autowired_WhenNoMarkedMember_DoesNothing()
    {
        using var provider = BuildProvider();
        var handler = new AutowiredServiceHandler(provider);
        var target = new AwsBareTarget();

        handler.Autowired(target);

        Assert.Null(target.PlainProperty);
    }

    /// <summary>
    /// 构建注册了被装配服务的服务提供器
    /// </summary>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IAwsDependency, AwsDependency>();
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// 自动装配测试用被注入契约
/// </summary>
internal interface IAwsDependency;

/// <summary>
/// 自动装配测试用被注入实现
/// </summary>
internal class AwsDependency : IAwsDependency;

/// <summary>
/// 自动装配测试目标
/// </summary>
internal class AwsTarget
{
    /// <summary>
    /// 带标记的字段
    /// </summary>
    [AutowiredService]
    public IAwsDependency? MarkedField;

    /// <summary>
    /// 未带标记的字段
    /// </summary>
    public IAwsDependency? PlainField;

    /// <summary>
    /// 带标记的属性
    /// </summary>
    [AutowiredService]
    public IAwsDependency? MarkedProperty { get; set; }

    /// <summary>
    /// 未带标记的属性
    /// </summary>
    public IAwsDependency? PlainProperty { get; set; }
}

/// <summary>
/// 无任何标记成员的自动装配目标
/// </summary>
internal class AwsBareTarget
{
    /// <summary>
    /// 未带标记的属性
    /// </summary>
    public IAwsDependency? PlainProperty { get; set; }
}
