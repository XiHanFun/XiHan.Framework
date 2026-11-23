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

    [Fact]
    public async Task Deliver_WhilePluginsAreLoadedAndUnloadedMidDelivery_FinishesWithoutThrowing()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var alpha = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run", Topic));
        var leaving = await AddAsync(roster, new StubPluginRuntime("xihan.test.leaving", "leavings.run", Topic));
        var omega = await AddAsync(roster, new StubPluginRuntime("xihan.test.omega", "omegas.run", Topic));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        alpha.OnDeliver = () =>
        {
            entered.TrySetResult();
            return release.Task;
        };

        // 投递停在第一个插件里：这期间卸载一个排在后面的订阅者，再加载一个新的订阅者
        var delivering = roster.DeliverAsync(Delivery(), Context()).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.True((await roster.WithdrawAsync("xihan.test.leaving", null, CancellationToken.None)).IsSuccess);
        var late = await AddAsync(roster, new StubPluginRuntime("xihan.test.late", "lates.run", Topic));
        release.SetResult(Result.Success());

        // 修之前投递沿着正在被增删的列表往下走，这里抛出 InvalidOperationException（Collection was modified）
        var delivered = await delivering.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.True(delivered.IsSuccess);
        // 已经移出名册的不再投（它正在停止，投过去只会得到不可用），开始投递之后才进来的也不投：这条消息按开始投递那一刻的名册走完
        Assert.Equal((1, 0, 1, 0), (alpha.Deliveries, leaving.Deliveries, omega.Deliveries, late.Deliveries));
    }

    [Fact]
    public async Task CheckAndDeliver_WhileRuntimesComeAndGo_NeverThrow_AndAlwaysSeeTheStableOnes()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var stable = new List<StubPluginRuntime>();
        for (var index = 0; index < 8; index++)
        {
            stable.Add(await AddAsync(roster, new StubPluginRuntime($"xihan.test.stable{index}", $"stables{index}.run", Topic)));
        }

        using var churning = new CancellationTokenSource();
        var delivery = Delivery();
        var context = Context();

        // 读者跑完第一轮各报一次：线程池忙的时候它们可能还没被调度，写者的三千轮就已经跑完了，那样增删与枚举根本没有交错
        var checkingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveringStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // 两个读者不停地取健康条目与投递，直到增删结束
        var checking = Task.Run(() =>
        {
            var rounds = 0;
            while (!churning.IsCancellationRequested)
            {
                var names = roster.Check().Select(static s => s.Name).ToHashSet(StringComparer.Ordinal);
                Assert.All(stable, runtime => Assert.Contains(PluginHealth.EntryPrefix + runtime.Manifest.Id, names));
                rounds++;
                checkingStarted.TrySetResult();
            }

            return rounds;
        });
        var delivering = Task.Run(async () =>
        {
            var rounds = 0;
            while (!churning.IsCancellationRequested)
            {
                Assert.True((await roster.DeliverAsync(delivery, context)).IsSuccess);
                rounds++;
                deliveringStarted.TrySetResult();
            }

            return rounds;
        });

        // 等两个读者都跑起来再开始增删；读者在第一轮就失败时把它的异常带出来，而不是等到超时
        var started = Task.WhenAll(checkingStarted.Task, deliveringStarted.Task);
        await await Task.WhenAny(started, checking, delivering).WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        // 写者：加载、卸载，以及运行期加载登记失败时的直接移除，三条路径都改登记顺序
        for (var round = 0; round < 3000; round++)
        {
            var transient = await AddAsync(roster, new StubPluginRuntime("xihan.test.transient", "transients.run", Topic));
            if (round % 2 == 0)
            {
                Assert.True((await roster.WithdrawAsync("xihan.test.transient", null, CancellationToken.None)).IsSuccess);
            }
            else
            {
                roster.Remove(transient);
            }
        }

        await churning.CancelAsync();
        // 修之前读者枚举的是正在被改的 List：这里以 InvalidOperationException 结束
        Assert.True(await checking.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None) > 0);
        Assert.True(await delivering.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None) > 0);
        Assert.Equal(stable, roster.Runtimes);
        Assert.All(stable, static runtime => Assert.True(runtime.Deliveries > 0));
    }

    [Fact]
    public async Task Remove_OfARuntimeThatWasAlreadyReplaced_LeavesTheCurrentOne()
    {
        using var roster = new PluginRoster<StubPluginRuntime>();
        var stale = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));
        Assert.True((await roster.WithdrawAsync("xihan.test.alpha", null, CancellationToken.None)).IsSuccess);
        var current = await AddAsync(roster, new StubPluginRuntime("xihan.test.alpha", "alphas.run"));

        // 运行期加载登记失败的收尾，晚于同一插件的卸载与再加载才到：修之前按清单 ID 移除，会把新的运行时从表里拿掉、却留在登记顺序里
        roster.Remove(stale);

        Assert.True(roster.TryFind("xihan.test.alpha", out var found));
        Assert.Same(current, found);
        Assert.Same(current, Assert.Single(roster.Runtimes));
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
