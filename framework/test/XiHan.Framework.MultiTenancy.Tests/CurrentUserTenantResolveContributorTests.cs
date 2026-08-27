// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Tests.Fakes;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 当前用户租户解析贡献者的测试
/// </summary>
/// <remarks>
/// 这个贡献者是解析链上优先级最高的一环（注册时被插到第 0 位），它的分支只有三条：
/// 未认证、已认证但没有租户（宿主用户）、已认证且带租户。
/// 前两条必须原样放行让后续贡献者接手，最后一条才写入上下文并短路——
/// 一旦前两条误置 Handled，宿主用户的请求会被钉死在无租户状态，Header/QueryString 解析全部失效。
/// </remarks>
public class CurrentUserTenantResolveContributorTests
{
    /// <summary>
    /// 贡献者名称常量不漂移
    /// </summary>
    /// <remarks>
    /// 名称会被用于在解析链里按名定位、替换或移除某个贡献者，属于对外可见的约定值。
    /// </remarks>
    [Fact]
    public void ContributorName_IsStable()
    {
        Assert.Equal("CurrentUser", CurrentUserTenantResolveContributor.ContributorName);
    }

    /// <summary>
    /// 实例名称取自名称常量
    /// </summary>
    [Fact]
    public void Name_ReturnsContributorName()
    {
        var contributor = new CurrentUserTenantResolveContributor();

        Assert.Equal(CurrentUserTenantResolveContributor.ContributorName, contributor.Name);
    }

    /// <summary>
    /// 继承自租户解析贡献者基类
    /// </summary>
    [Fact]
    public void Type_DerivesFromContributorBase()
    {
        var contributor = new CurrentUserTenantResolveContributor();

        Assert.IsAssignableFrom<TenantResolveContributorBase>(contributor);
        Assert.IsAssignableFrom<ITenantResolveContributor>(contributor);
    }

    /// <summary>
    /// 已认证且带租户时写入租户标识并短路解析链
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenAuthenticatedWithTenant_SetsTenantIdAndHandled()
    {
        var context = CreateContext(new FakeCurrentUser { IsAuthenticated = true, TenantId = 42L });
        var contributor = new CurrentUserTenantResolveContributor();

        await contributor.ResolveAsync(context);

        Assert.Equal("42", context.TenantIdOrName);
        Assert.True(context.Handled);
    }

    /// <summary>
    /// 任意合法租户标识都能被解析成对应的字符串
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public async Task ResolveAsync_WithAnyTenantId_WritesItsStringForm(long tenantId)
    {
        var context = CreateContext(new FakeCurrentUser { IsAuthenticated = true, TenantId = tenantId });
        var contributor = new CurrentUserTenantResolveContributor();

        await contributor.ResolveAsync(context);

        Assert.Equal(tenantId.ToString(), context.TenantIdOrName);
        Assert.True(context.Handled);
    }

    /// <summary>
    /// 未认证时原样放行，交给后续贡献者
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenNotAuthenticated_LeavesContextUnhandled()
    {
        var context = CreateContext(new FakeCurrentUser { IsAuthenticated = false, TenantId = 42L });
        var contributor = new CurrentUserTenantResolveContributor();

        await contributor.ResolveAsync(context);

        Assert.Null(context.TenantIdOrName);
        Assert.False(context.Handled);
    }

    /// <summary>
    /// 已认证但没有租户（宿主用户）时原样放行
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenAuthenticatedWithoutTenant_LeavesContextUnhandled()
    {
        var context = CreateContext(new FakeCurrentUser { IsAuthenticated = true, TenantId = null });
        var contributor = new CurrentUserTenantResolveContributor();

        await contributor.ResolveAsync(context);

        Assert.Null(context.TenantIdOrName);
        Assert.False(context.Handled);
    }

    /// <summary>
    /// 不命中时不会抹掉上下文里已有的租户标识
    /// </summary>
    /// <remarks>
    /// 解析链可能被重排，这个贡献者未必总是第一个执行；不命中就必须完全不碰上下文。
    /// </remarks>
    [Fact]
    public async Task ResolveAsync_WhenMissing_DoesNotOverwriteExistingValue()
    {
        var context = CreateContext(new FakeCurrentUser { IsAuthenticated = false });
        context.TenantIdOrName = "前一个贡献者的结果";
        var contributor = new CurrentUserTenantResolveContributor();

        await contributor.ResolveAsync(context);

        Assert.Equal("前一个贡献者的结果", context.TenantIdOrName);
        Assert.False(context.Handled);
    }

    /// <summary>
    /// 容器中缺少当前用户服务时直接抛出无效操作异常
    /// </summary>
    /// <remarks>
    /// 这里用的是 GetRequiredService 而非 GetService，缺注册属于装配错误而不是「解析不到租户」，
    /// 必须快速失败而不是静默跳过——否则会在生产上表现为「所有请求都落到宿主」。
    /// </remarks>
    [Fact]
    public async Task ResolveAsync_WhenCurrentUserNotRegistered_ThrowsInvalidOperationException()
    {
        var context = new FakeTenantResolveContext(new ServiceCollection().BuildServiceProvider());
        var contributor = new CurrentUserTenantResolveContributor();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await contributor.ResolveAsync(context));
    }

    /// <summary>
    /// 同一个贡献者实例可以重复解析，不残留状态
    /// </summary>
    /// <remarks>
    /// 贡献者实例被登记在单例选项里，会被所有请求共享，因此必须无状态。
    /// </remarks>
    [Fact]
    public async Task ResolveAsync_ReusedAcrossContexts_KeepsNoState()
    {
        var contributor = new CurrentUserTenantResolveContributor();

        var tenantContext = CreateContext(new FakeCurrentUser { IsAuthenticated = true, TenantId = 42L });
        await contributor.ResolveAsync(tenantContext);

        var hostContext = CreateContext(new FakeCurrentUser { IsAuthenticated = true, TenantId = null });
        await contributor.ResolveAsync(hostContext);

        Assert.Equal("42", tenantContext.TenantIdOrName);
        Assert.True(tenantContext.Handled);
        Assert.Null(hostContext.TenantIdOrName);
        Assert.False(hostContext.Handled);
    }

    /// <summary>
    /// 创建注册了指定当前用户的解析上下文
    /// </summary>
    /// <param name="currentUser">当前用户替身</param>
    /// <returns>租户解析上下文</returns>
    private static FakeTenantResolveContext CreateContext(FakeCurrentUser currentUser)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(currentUser);
        return new FakeTenantResolveContext(services.BuildServiceProvider());
    }
}
