// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 服务激活回调列表与激活上下文测试
/// </summary>
/// <remarks>
/// 激活回调按「服务描述器实例」精确匹配（引用相等），
/// 内容等价但实例不同的描述器必须取不到回调，否则同一服务类型的多条注册会互相串扰。
/// </remarks>
public class ServiceActivatedActionListTests
{
    /// <summary>
    /// 按描述器取出全部匹配回调并保持加入顺序
    /// </summary>
    [Fact]
    public void GetActions_ReturnsMatchingActionsInOrder()
    {
        var descriptor = ServiceDescriptor.Describe(typeof(ISaaContract), typeof(SaaService), ServiceLifetime.Transient);
        var list = new ServiceActivatedActionList();
        List<string> calls = [];
        list.Add(new KeyValuePair<ServiceDescriptor, Action<IOnServiceActivatedContext>>(descriptor, _ => calls.Add("first")));
        list.Add(new KeyValuePair<ServiceDescriptor, Action<IOnServiceActivatedContext>>(descriptor, _ => calls.Add("second")));

        var actions = list.GetActions(descriptor);

        Assert.Equal(2, actions.Count);
        foreach (var action in actions)
        {
            action(new OnServiceActivatedContext(new SaaService()));
        }

        Assert.Equal("first", calls[0]);
        Assert.Equal("second", calls[1]);
    }

    /// <summary>
    /// 描述器未登记回调时返回空列表
    /// </summary>
    [Fact]
    public void GetActions_WhenNoMatch_ReturnsEmpty()
    {
        var registered = ServiceDescriptor.Describe(typeof(ISaaContract), typeof(SaaService), ServiceLifetime.Transient);
        var other = ServiceDescriptor.Describe(typeof(ISaaContract), typeof(SaaService), ServiceLifetime.Transient);
        var list = new ServiceActivatedActionList
        {
            new KeyValuePair<ServiceDescriptor, Action<IOnServiceActivatedContext>>(registered, _ => { })
        };

        Assert.Empty(list.GetActions(other));
    }

    /// <summary>
    /// 激活上下文原样携带被激活实例
    /// </summary>
    [Fact]
    public void OnServiceActivatedContext_KeepsInstance()
    {
        var instance = new SaaService();

        var context = new OnServiceActivatedContext(instance);

        Assert.Same(instance, context.Instance);
    }
}

/// <summary>
/// 激活回调测试用契约
/// </summary>
internal interface ISaaContract;

/// <summary>
/// 激活回调测试用实现
/// </summary>
internal class SaaService : ISaaContract;
