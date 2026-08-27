// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 租户解析贡献者基类的测试
/// </summary>
/// <remarks>
/// 基类本身只是把抽象契约收敛成一个可继承的模板，没有任何逻辑，
/// 所以用例分两层：一层用最小具体子类（<see cref="RecordingTenantResolveContributor"/>）验证模板形状能落地，
/// 另一层把多个贡献者串成链，验证 Handled 的首命中短路语义与全部未命中的收尾状态。
/// 链条的驱动器本身不在本包内（由 Web 侧中间件承担），这里按契约手写一份最小驱动逻辑，
/// 钉住的是「本包提供的贡献者与选项集合被这样消费」这条约定，而不是某个具体中间件的实现。
/// </remarks>
public class TenantResolveContributorBaseTests
{
    /// <summary>
    /// 基类是抽象类且实现了解析贡献者契约
    /// </summary>
    [Fact]
    public void Type_IsAbstractAndImplementsContributorContract()
    {
        Assert.True(typeof(TenantResolveContributorBase).IsAbstract);
        Assert.True(typeof(ITenantResolveContributor).IsAssignableFrom(typeof(TenantResolveContributorBase)));
    }

    /// <summary>
    /// 名称与解析方法都留给子类实现
    /// </summary>
    /// <remarks>
    /// 基类若给了默认实现，子类忘记覆写时会静默退化成「永不命中」，链条排障成本极高，
    /// 因此把两个成员必须是抽象这件事显式钉住。
    /// </remarks>
    [Fact]
    public void Members_AreAbstract()
    {
        var nameProperty = typeof(TenantResolveContributorBase).GetProperty(nameof(TenantResolveContributorBase.Name));
        var resolveMethod = typeof(TenantResolveContributorBase).GetMethod(nameof(TenantResolveContributorBase.ResolveAsync));

        Assert.NotNull(nameProperty);
        Assert.NotNull(resolveMethod);
        Assert.NotNull(nameProperty.GetMethod);
        Assert.True(nameProperty.GetMethod.IsAbstract);
        Assert.False(nameProperty.CanWrite);
        Assert.True(resolveMethod.IsAbstract);
        Assert.Equal(typeof(Task), resolveMethod.ReturnType);
    }

    /// <summary>
    /// 最小子类可以正常暴露名称
    /// </summary>
    [Fact]
    public void Name_FromConcreteSubclass_IsExposedThroughContract()
    {
        ITenantResolveContributor contributor = new RecordingTenantResolveContributor("Header");

        Assert.Equal("Header", contributor.Name);
    }

    /// <summary>
    /// 命中的贡献者写入租户标识并置为已处理
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenContributorHits_SetsTenantIdOrNameAndHandled()
    {
        var context = CreateContext();
        var contributor = new RecordingTenantResolveContributor("Header", "42");

        await contributor.ResolveAsync(context);

        Assert.Equal("42", context.TenantIdOrName);
        Assert.True(context.Handled);
        Assert.Equal(1, contributor.ResolveCallCount);
    }

    /// <summary>
    /// 未命中的贡献者不改动上下文
    /// </summary>
    /// <remarks>
    /// 未命中却把 Handled 置真，会让链条提前短路并让后续贡献者永远没有机会执行；
    /// 未命中却清空 TenantIdOrName，则会抹掉前一个贡献者的成果。两条都必须为假才算正确的「不命中」。
    /// </remarks>
    [Fact]
    public async Task ResolveAsync_WhenContributorMisses_LeavesContextUntouched()
    {
        var context = CreateContext();
        context.TenantIdOrName = "既有值";
        var contributor = new RecordingTenantResolveContributor("Header");

        await contributor.ResolveAsync(context);

        Assert.Equal("既有值", context.TenantIdOrName);
        Assert.False(context.Handled);
        Assert.Equal(1, contributor.ResolveCallCount);
    }

    /// <summary>
    /// 解析链在首个命中处短路，后续贡献者不再执行
    /// </summary>
    [Fact]
    public async Task ResolveChain_StopsAtFirstHit()
    {
        var context = CreateContext();
        var header = new RecordingTenantResolveContributor("Header");
        var queryString = new RecordingTenantResolveContributor("QueryString", "7");
        var cookie = new RecordingTenantResolveContributor("Cookie", "9");

        await RunResolveChainAsync([header, queryString, cookie], context);

        Assert.Equal("7", context.TenantIdOrName);
        Assert.True(context.Handled);
        Assert.Equal(1, header.ResolveCallCount);
        Assert.Equal(1, queryString.ResolveCallCount);
        Assert.Equal(0, cookie.ResolveCallCount);
    }

    /// <summary>
    /// 全部未命中时链条跑完且上下文保持未处理
    /// </summary>
    [Fact]
    public async Task ResolveChain_WhenNoContributorHits_LeavesContextUnhandled()
    {
        var context = CreateContext();
        var contributors = new[]
        {
            new RecordingTenantResolveContributor("Header"),
            new RecordingTenantResolveContributor("QueryString"),
            new RecordingTenantResolveContributor("Cookie")
        };

        await RunResolveChainAsync(contributors, context);

        Assert.Null(context.TenantIdOrName);
        Assert.False(context.Handled);
        Assert.All(contributors, contributor => Assert.Equal(1, contributor.ResolveCallCount));
    }

    /// <summary>
    /// 空解析链不会抛异常，上下文保持未处理
    /// </summary>
    [Fact]
    public async Task ResolveChain_WhenEmpty_LeavesContextUnhandled()
    {
        var context = CreateContext();

        await RunResolveChainAsync([], context);

        Assert.Null(context.TenantIdOrName);
        Assert.False(context.Handled);
    }

    /// <summary>
    /// 解析链按选项集合中登记的顺序执行
    /// </summary>
    /// <remarks>
    /// 选项里的顺序就是优先级，插到最前面的贡献者必须最先拿到解析机会。
    /// </remarks>
    [Fact]
    public async Task ResolveChain_FollowsOrderDeclaredInOptions()
    {
        var context = CreateContext();
        var options = new XiHanTenantResolveOptions();
        var fallback = new RecordingTenantResolveContributor("Fallback", "999");
        var preferred = new RecordingTenantResolveContributor("Preferred", "1");

        options.TenantResolvers.Add(fallback);
        options.TenantResolvers.Insert(0, preferred);

        await RunResolveChainAsync(options.TenantResolvers, context);

        Assert.Equal("1", context.TenantIdOrName);
        Assert.Equal(1, preferred.ResolveCallCount);
        Assert.Equal(0, fallback.ResolveCallCount);
    }

    /// <summary>
    /// 按契约驱动一条解析链：逐个执行，遇到已处理即停止
    /// </summary>
    /// <param name="contributors">解析贡献者集合</param>
    /// <param name="context">租户解析上下文</param>
    /// <returns></returns>
    private static async Task RunResolveChainAsync(IEnumerable<ITenantResolveContributor> contributors, ITenantResolveContext context)
    {
        foreach (var contributor in contributors)
        {
            await contributor.ResolveAsync(context);

            if (context.Handled)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 创建带空容器的解析上下文
    /// </summary>
    /// <returns>租户解析上下文</returns>
    private static FakeTenantResolveContext CreateContext()
    {
        return new FakeTenantResolveContext(new ServiceCollection().BuildServiceProvider());
    }
}
