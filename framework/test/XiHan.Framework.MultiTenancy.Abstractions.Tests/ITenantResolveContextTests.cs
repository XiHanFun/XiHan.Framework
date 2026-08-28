// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 租户解析上下文契约的测试
/// </summary>
/// <remarks>
/// 上下文是解析链上唯一的可变载体：贡献者往里写 <see cref="ITenantResolveContext.TenantIdOrName"/> 并置位
/// <see cref="ITenantResolveContext.Handled"/>，同时通过继承自 Core 的服务提供者访问器契约拿到容器。
/// 这里锁的是「两个字段可读可写、初始为空、容器可用」这三条前提。
/// </remarks>
public class ITenantResolveContextTests
{
    /// <summary>
    /// 新建的上下文处于未解析状态
    /// </summary>
    [Fact]
    public void NewContext_HasNoTenantAndIsNotHandled()
    {
        var context = CreateContext(out var provider);

        using (provider)
        {
            Assert.Null(context.TenantIdOrName);
            Assert.False(context.Handled);
        }
    }

    /// <summary>
    /// 租户唯一标识或名称可被写入，也可被清空
    /// </summary>
    /// <remarks>
    /// 契约用的是字符串而不是长整型：解析阶段拿到的可能是租户名，尚未换算成唯一标识，清空能力用于贡献者纠错回退。
    /// </remarks>
    [Fact]
    public void TenantIdOrName_CanBeAssignedAndCleared()
    {
        var context = CreateContext(out var provider);

        using (provider)
        {
            context.TenantIdOrName = "acme";
            Assert.Equal("acme", context.TenantIdOrName);

            context.TenantIdOrName = null;
            Assert.Null(context.TenantIdOrName);
        }
    }

    /// <summary>
    /// 已处理标记可被置位与复位
    /// </summary>
    [Fact]
    public void Handled_CanBeToggled()
    {
        var context = CreateContext(out var provider);

        using (provider)
        {
            context.Handled = true;
            Assert.True(context.Handled);

            context.Handled = false;
            Assert.False(context.Handled);
        }
    }

    /// <summary>
    /// 上下文必须能把容器里的服务交给解析贡献者
    /// </summary>
    [Fact]
    public void ServiceProvider_ResolvesRegisteredService()
    {
        var services = new ServiceCollection();
        var accessor = new FakeCurrentTenantAccessor();
        services.AddSingleton<ICurrentTenantAccessor>(accessor);

        using var provider = services.BuildServiceProvider();
        ITenantResolveContext context = new FakeTenantResolveContext(provider);

        var resolved = context.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();

        Assert.Same(accessor, resolved);
    }

    /// <summary>
    /// 契约继承自服务提供者访问器
    /// </summary>
    /// <remarks>
    /// 这条继承关系决定了解析贡献者不需要额外注入容器，断开它会波及所有贡献者实现。
    /// </remarks>
    [Fact]
    public void Contract_InheritsServiceProviderAccessor()
    {
        Assert.True(typeof(IServiceProviderAccessor).IsAssignableFrom(typeof(ITenantResolveContext)));
    }

    /// <summary>
    /// 契约要求两个状态字段均可读可写
    /// </summary>
    [Fact]
    public void Contract_StateProperties_AreMutable()
    {
        var tenantIdOrName = typeof(ITenantResolveContext).GetProperty(nameof(ITenantResolveContext.TenantIdOrName));
        var handled = typeof(ITenantResolveContext).GetProperty(nameof(ITenantResolveContext.Handled));

        Assert.NotNull(tenantIdOrName);
        Assert.NotNull(handled);
        Assert.Equal(typeof(string), tenantIdOrName.PropertyType);
        Assert.Equal(typeof(bool), handled.PropertyType);
        Assert.True(tenantIdOrName.CanRead);
        Assert.True(tenantIdOrName.CanWrite);
        Assert.True(handled.CanRead);
        Assert.True(handled.CanWrite);
    }

    /// <summary>
    /// 创建带空容器的租户解析上下文
    /// </summary>
    /// <param name="provider">供调用方释放的服务提供者</param>
    /// <returns>租户解析上下文</returns>
    private static ITenantResolveContext CreateContext(out ServiceProvider provider)
    {
        provider = new ServiceCollection().BuildServiceProvider();
        return new FakeTenantResolveContext(provider);
    }
}
