// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 对象访问器测试
/// </summary>
/// <remarks>
/// 对象访问器是「先占位、后赋值」的容器，模块装配期把尚未创建的服务提供器等对象先登记进服务集合，
/// 构建完成后再回填。这里验证延迟赋值语义、协变读取契约，以及同类型只允许登记一次的约束。
/// </remarks>
public class ObjectAccessorTests
{
    /// <summary>
    /// 无参构造后值为空
    /// </summary>
    [Fact]
    public void Constructor_WhenEmpty_ValueIsNull()
    {
        var accessor = new ObjectAccessor<OaPayload>();

        Assert.Null(accessor.Value);
    }

    /// <summary>
    /// 带值构造后值为传入对象
    /// </summary>
    [Fact]
    public void Constructor_WithValue_KeepsGivenObject()
    {
        var payload = new OaPayload();

        var accessor = new ObjectAccessor<OaPayload>(payload);

        Assert.Same(payload, accessor.Value);
    }

    /// <summary>
    /// 占位后回填的值对已持有引用的一方可见
    /// </summary>
    [Fact]
    public void Value_WhenAssignedLater_IsVisibleThroughReadOnlyView()
    {
        var accessor = new ObjectAccessor<OaPayload>();
        IObjectAccessor<OaPayload> readOnlyView = accessor;
        var payload = new OaPayload();

        accessor.Value = payload;

        Assert.Same(payload, readOnlyView.Value);
    }

    /// <summary>
    /// 登记的访问器同时以自身与只读接口两种服务类型可解析
    /// </summary>
    [Fact]
    public void AddObjectAccessor_RegistersBothConcreteAndInterfaceService()
    {
        IServiceCollection services = new ServiceCollection();
        var payload = new OaPayload();

        services.AddObjectAccessor(payload);

        using var provider = services.BuildServiceProvider();
        Assert.Same(payload, provider.GetRequiredService<ObjectAccessor<OaPayload>>().Value);
        Assert.Same(payload, provider.GetRequiredService<IObjectAccessor<OaPayload>>().Value);
    }

    /// <summary>
    /// 同类型重复登记访问器时抛出
    /// </summary>
    [Fact]
    public void AddObjectAccessor_WhenAlreadyRegistered_Throws()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddObjectAccessor<OaPayload>();

        Assert.Throws<Exception>(() =>
        {
            _ = services.AddObjectAccessor<OaPayload>();
        });
    }

    /// <summary>
    /// 尝试登记访问器时复用已有实例
    /// </summary>
    [Fact]
    public void TryAddObjectAccessor_WhenAlreadyRegistered_ReturnsExistingAccessor()
    {
        IServiceCollection services = new ServiceCollection();
        var first = services.TryAddObjectAccessor<OaPayload>();

        var second = services.TryAddObjectAccessor<OaPayload>();

        Assert.Same(first, second);
    }

    /// <summary>
    /// 从服务集合取对象时按登记情况返回值或空
    /// </summary>
    [Fact]
    public void GetObjectOrNull_ReflectsAccessorState()
    {
        IServiceCollection services = new ServiceCollection();
        var accessor = services.AddObjectAccessor<OaPayload>();

        Assert.Null(services.GetObjectOrNull<OaPayload>());

        var payload = new OaPayload();
        accessor.Value = payload;

        Assert.Same(payload, services.GetObjectOrNull<OaPayload>());
        Assert.Same(payload, services.GetObject<OaPayload>());
    }
}

/// <summary>
/// 对象访问器测试载荷
/// </summary>
internal class OaPayload;
