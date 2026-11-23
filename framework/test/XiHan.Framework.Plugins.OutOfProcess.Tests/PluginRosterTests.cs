// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 两个宿主共用的插件名册：登记、撤回、事件投递与健康条目
/// </summary>
/// <remarks>
/// 规则出自 ADR-0085、ADR-0088 与 ADR-0260。用假运行时单独核对名册的规则；真实载体上的加载、卸载与并发调用仍由两个宿主的端到端用例覆盖
/// </remarks>
public sealed class PluginRosterTests
{
    /// <summary>
    /// 假运行时订阅的主题
    /// </summary>
    private const string Topic = "stub.events";

    [Fact]
    public async Task Add_RefusesASecondRuntimeWithTheSameManifestId()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var first = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        var created = false;

        var second = await roster.AddAsync(
            "xihan.test.alpha",
            () =>
            {
                created = true;
                return new StubPluginRuntime("xihan.test.alpha", "alphas.run");
            },
            CancellationToken.None);

        Assert.Equal(PluginErrors.AlreadyLoaded, second.Error?.Code);
        // 冲突时不建运行时：载体的资源不该为一个进不了名册的插件占上
        Assert.False(created);
        Assert.Same(first, Assert.Single(roster.Runtimes));
    }

    [Fact]
    public async Task Register_ThenWithdraw_PutsTheOperationInTheRegistryAndTakesItOut()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var registry = UseRegistry(roster);
        var alpha = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));

        Assert.True(roster.Register(alpha, null).IsSuccess);
        Assert.Equal("xihan.test.alpha", registry.Find("alphas.run").ValueOrThrow().OwnerId);
        Assert.Equal(PluginHealth.EntryPrefix + "xihan.test.alpha", Assert.Single(roster.Check()).Name);

        var withdrawn = await roster.WithdrawAsync("xihan.test.alpha", null, CancellationToken.None);

        Assert.Same(alpha, withdrawn.ValueOrThrow());
        Assert.False(registry.Find("alphas.run").HasValue);
        Assert.False(roster.TryFind("xihan.test.alpha", out _));
        Assert.Empty(roster.Runtimes);
        Assert.Empty(roster.Check());
        // 撤回之后同一个清单 ID 可以直接再进来、再登记
        var again = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        Assert.True(roster.Register(again, null).IsSuccess);
    }

    [Fact]
    public async Task Withdraw_OfAnUnknownPlugin_IsNotFound()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();

        var withdrawn = await roster.WithdrawAsync("xihan.test.missing", null, CancellationToken.None);

        Assert.Equal(PluginErrors.NotFound, withdrawn.Error?.Code);
        Assert.Equal(PluginMessages.NotLoaded, withdrawn.Error?.MessageKey);
    }

    [Fact]
    public async Task Register_OfARuntimeThatIsNoLongerTheCurrentOne_IsRefusedAndRegistersNothing()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var registry = UseRegistry(roster);
        var stale = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        Assert.True((await roster.WithdrawAsync("xihan.test.alpha", null, CancellationToken.None)).IsSuccess);

        // 卸载之后才握手成功的重试：运行时已经不在名册里
        Assert.Equal(PluginErrors.NotFound, roster.Register(stale, null).Error?.Code);
        Assert.Equal(0, registry.Count);

        // 同一个清单 ID 已经换成新的运行时：旧的照样登记不进去，不会抢在新的前面占住操作名
        var current = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        Assert.Equal(PluginErrors.NotFound, roster.Register(stale, null).Error?.Code);
        Assert.Equal(0, registry.Count);
        Assert.True(roster.Register(current, null).IsSuccess);
    }

    [Fact]
    public async Task Register_WhenTheCarrierFollowUpFails_TakesBackWhatWasJustRegistered()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var registry = UseRegistry(roster);
        var alpha = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        var refused = Error.Literal(KernelErrors.PipelineRegistration, "The middleware constraint cannot be resolved.");
        var registeredWhenFollowingUp = false;

        var registered = roster.Register(alpha, _ =>
        {
            // 追加项在操作登记成功之后才做
            registeredWhenFollowingUp = registry.Find("alphas.run").HasValue;
            return refused;
        });

        Assert.Same(refused, registered.Error);
        Assert.True(registeredWhenFollowingUp);
        Assert.Equal(0, registry.Count);
        // 登记不进去不等于移出名册：由宿主停止运行时后再移除
        Assert.True(roster.TryFind("xihan.test.alpha", out _));
    }

    [Fact]
    public async Task Withdraw_WhenTheCarrierRefuses_ChangesNothing()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var registry = UseRegistry(roster);
        var alpha = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        Assert.True(roster.Register(alpha, null).IsSuccess);
        var refused = Error.Literal(KernelErrors.PipelineRegistration, "Another middleware is ordered against it.");

        var withdrawn = await roster.WithdrawAsync("xihan.test.alpha", _ => refused, CancellationToken.None);

        Assert.Same(refused, withdrawn.Error);
        Assert.True(roster.TryFind("xihan.test.alpha", out var kept));
        Assert.Same(alpha, kept);
        Assert.Same(alpha, Assert.Single(roster.Runtimes));
        Assert.True(registry.Find("alphas.run").HasValue);
    }

    [Fact]
    public async Task Deliver_GoesToTheSubscribedRuntimesInOrder_AndTheFirstFailureIsTheResult()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var alpha = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run", Topic));
        var bystander = await AddAsync(roster, new StubPluginRuntime("xihan.test.bystander", "bystanders.run", "other.events"));
        var failing = await AddAsync(roster, new StubPluginRuntime("xihan.test.failing", "failings.run", Topic));
        var afterwards = await AddAsync(roster, new StubPluginRuntime("xihan.test.omega", "omegas.run", Topic));
        var failure = Error.Literal(PluginErrors.Unexpected, "The handler failed.");
        failing.OnDeliver = () => Task.FromResult(Result.Failure(failure));

        var delivered = await roster.DeliverAsync(Delivery(), Context());

        Assert.Same(failure, delivered.Error);
        Assert.Equal((1, 0, 1, 0), (alpha.Deliveries, bystander.Deliveries, failing.Deliveries, afterwards.Deliveries));
    }

    [Fact]
    public async Task Check_ReportsEveryRuntimeWithItsStateAndError_AndUnreadableDirectoriesByName()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var alpha = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        var crashed = Error.Literal(PluginErrors.Crashed, "The process exited.");
        alpha.State = LifecycleState.Failed;
        alpha.Error = crashed;
        var unreadable = Error.Literal(PluginErrors.InvalidSource, "The manifest is not JSON.");
        roster.AddUnreadable(Path.Combine(Path.GetTempPath(), "broken-plugin"), unreadable);

        var statuses = roster.Check();

        Assert.Equal(2, statuses.Count);
        Assert.Equal(("plugins.xihan.test.alpha", LifecycleState.Failed, crashed), (statuses[0].Name, statuses[0].State, statuses[0].Error));
        Assert.Equal(("plugins.broken-plugin", LifecycleState.Failed, unreadable), (statuses[1].Name, statuses[1].State, statuses[1].Error));
    }

    /// <summary>
    /// 把一个假运行时放进名册
    /// </summary>
    private static async Task<StubPluginRuntime> AddAsync(PluginRoster<StubPluginRuntime> roster, StubPluginRuntime runtime)
        => (await roster.AddAsync(runtime.Manifest.Id, () => runtime, CancellationToken.None)).ValueOrThrow();

    /// <summary>
    /// 给名册一张空的操作登记表
    /// </summary>
    private static OperationRegistry UseRegistry(PluginRoster<StubPluginRuntime> roster)
    {
        var registry = new OperationRegistry(new PermissionRegistry());
        roster.UseRegistry(registry);
        return registry;
    }

    /// <summary>
    /// 发到假运行时订阅的主题上的一次竞争投递
    /// </summary>
    private static MessageDelivery Delivery()
        => new()
        {
            Topic = Topic,
            Envelope = new MessageEnvelope { Id = "evt-1", Source = "tests", Type = "stub.created", Data = "{}"u8.ToArray() },
            Attempt = 1,
            Mode = SubscriptionMode.Competing,
        };

    /// <summary>
    /// 一个不带截止时间的调用上下文
    /// </summary>
    private static InvocationContext Context()
        => App.CreateContext(new ServiceCollection().BuildServiceProvider(), new InvocationContextOptions(), CancellationToken.None);
}
