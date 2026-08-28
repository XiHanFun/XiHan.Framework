// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 租户解析贡献者契约的测试
/// </summary>
/// <remarks>
/// 贡献者本身是一串「按顺序尝试、谁先命中谁定案」的责任链，抽象包只给出单个节点的契约。
/// 这里用手写替身把整条链跑一遍，锁死链式语义：命中后置位 Handled，后续节点仍会被调用但不得覆盖已有结果。
/// </remarks>
public class ITenantResolveContributorTests
{
    /// <summary>
    /// 命中时写入解析结果并置位已处理
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenMatched_SetsTenantIdOrNameAndMarksHandled()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        ITenantResolveContext context = new FakeTenantResolveContext(provider);
        ITenantResolveContributor contributor = new FakeTenantResolveContributor("查询串", "acme");

        await contributor.ResolveAsync(context);

        Assert.Equal("acme", context.TenantIdOrName);
        Assert.True(context.Handled);
    }

    /// <summary>
    /// 解析不出租户时不置位已处理，把机会留给下一个贡献者
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenNotMatched_LeavesContextUnhandled()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        ITenantResolveContext context = new FakeTenantResolveContext(provider);
        ITenantResolveContributor contributor = new FakeTenantResolveContributor("请求头");

        await contributor.ResolveAsync(context);

        Assert.Null(context.TenantIdOrName);
        Assert.False(context.Handled);
    }

    /// <summary>
    /// 上下文已被前序贡献者处理时不得覆盖已有结果
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenAlreadyHandled_DoesNotOverwriteResult()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        ITenantResolveContext context = new FakeTenantResolveContext(provider)
        {
            TenantIdOrName = "acme",
            Handled = true
        };
        var contributor = new FakeTenantResolveContributor("Cookie", "other");

        await contributor.ResolveAsync(context);

        Assert.Equal("acme", context.TenantIdOrName);
        Assert.True(context.Handled);
        Assert.Equal(1, contributor.InvokeCount);
    }

    /// <summary>
    /// 责任链在第一个命中的贡献者处定案，后续贡献者不改写结果
    /// </summary>
    /// <remarks>
    /// 断言里同时检查了后两个节点的调用次数：链本身不做短路裁剪，短路是靠每个节点自己看 Handled 实现的，
    /// 这个分工一旦被改成「命中即中断遍历」，日志/诊断类贡献者就再也拿不到执行机会。
    /// </remarks>
    [Fact]
    public async Task ResolveAsync_InChain_FirstMatchWins()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        ITenantResolveContext context = new FakeTenantResolveContext(provider);

        var header = new FakeTenantResolveContributor("请求头");
        var query = new FakeTenantResolveContributor("查询串", "acme");
        var cookie = new FakeTenantResolveContributor("Cookie", "contoso");
        var chain = new List<ITenantResolveContributor> { header, query, cookie };

        foreach (var contributor in chain)
        {
            await contributor.ResolveAsync(context);
        }

        Assert.Equal("acme", context.TenantIdOrName);
        Assert.True(context.Handled);
        Assert.Equal(1, header.InvokeCount);
        Assert.Equal(1, query.InvokeCount);
        Assert.Equal(1, cookie.InvokeCount);
    }

    /// <summary>
    /// 名称是贡献者的稳定标识，不能为空
    /// </summary>
    [Fact]
    public void Name_IsNonEmptyIdentifier()
    {
        ITenantResolveContributor contributor = new FakeTenantResolveContributor("查询串", "acme");

        Assert.False(string.IsNullOrWhiteSpace(contributor.Name));
        Assert.Equal("查询串", contributor.Name);
    }

    /// <summary>
    /// 契约要求名称只读、解析方法返回非泛型 Task 且只接受上下文一个参数
    /// </summary>
    [Fact]
    public void Contract_Shape_IsNameAndResolveAsync()
    {
        var name = typeof(ITenantResolveContributor).GetProperty(nameof(ITenantResolveContributor.Name));
        var method = typeof(ITenantResolveContributor).GetMethod(nameof(ITenantResolveContributor.ResolveAsync));

        Assert.NotNull(name);
        Assert.Equal(typeof(string), name.PropertyType);
        Assert.True(name.CanRead);
        Assert.False(name.CanWrite);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);

        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(ITenantResolveContext), parameter.ParameterType);
    }
}
