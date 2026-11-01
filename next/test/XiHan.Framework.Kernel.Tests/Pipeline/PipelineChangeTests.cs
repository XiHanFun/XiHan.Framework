// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Pipeline;

/// <summary>
/// 管道的成组登记与按归属撤回。
/// </summary>
/// <remarks>
/// RFC-0004「运行期改动」、ADR-0088：一组全进或全不进、撤回不留下悬空约束、应用构建之后只能成组改动；每次调用按进入管道时的委托走完，之后开始的调用经过改动后的管道。
/// </remarks>
public sealed class PipelineChangeTests
{
    /// <summary>
    /// 插件的归属。
    /// </summary>
    private const string Plugin = "xihan.plugins.p";

    [Fact]
    public void RemoveOwnedBy_WithdrawsTheGroup_AndUseOwnedPutsItBackInTheSamePlace()
    {
        var log = new List<string>();
        var builder = new PipelineBuilder()
            .UseNamedInstance("p:first", new MiddlewareA(log), PipelineStage.Observe, owner: Plugin)
            .UseAt<MiddlewareB>(PipelineStage.Observe, owner: "xihan.a")
            .UseNamedInstance("p:second", new MiddlewareA(log), PipelineStage.Observe, owner: Plugin)
            .Before("p:second", typeof(MiddlewareB).FullName!);
        var original = builder.Dump();

        // 同一归属的两个中间件连同挂在它们上面的约束一次撤回，别的归属不动
        Assert.Equal(2, builder.RemoveOwnedBy(Plugin));
        Assert.Equal($"observe      {typeof(MiddlewareB).FullName}\n", builder.Dump());
        Assert.Equal(0, builder.RemoveOwnedBy(Plugin));

        // 同名再登记不撞上自己留下的登记，排序规则把它放回原来的位置
        builder.UseOwned(Plugin, static p => p
            .UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .UseNamedInstance("p:second", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .Before("p:second", typeof(MiddlewareB).FullName!));
        Assert.Equal(original, builder.Dump());
    }

    [Fact]
    public void UseOwned_ThatCannotBeOrdered_LeavesThePipelineUnchanged()
    {
        var builder = new PipelineBuilder().UseAt<MiddlewareB>(PipelineStage.Observe, owner: "xihan.a");
        var original = builder.Dump();

        // 目标未登记：回调里的单项登记都成功了，回调之后的核对整组撤掉
        var missing = Assert.Throws<FrameworkException>(() => builder.UseOwned(Plugin, static p => p
            .UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .After("p:first", "xihan.other:missing")));
        Assert.Equal(KernelErrors.PipelineRegistration, missing.Code);
        Assert.Equal(original, builder.Dump());

        // 组内成环
        var cycle = Assert.Throws<FrameworkException>(() => builder.UseOwned(Plugin, static p => p
            .UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .UseNamedInstance("p:second", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .Before("p:first", "p:second")
            .Before("p:second", "p:first")));
        Assert.Equal(KernelErrors.PipelineCycle, cycle.Code);
        Assert.Equal(original, builder.Dump());

        // 撤掉的名字可以再登记：去重索引也一并还原
        builder.UseOwned(Plugin, static p => p.UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin));
        Assert.Contains("[p:first]", builder.Dump(), StringComparison.Ordinal);
    }

    [Fact]
    public void UseOwned_RejectsOtherOwners_ConstraintsOutsideTheGroup_AndNesting()
    {
        var builder = new PipelineBuilder().UseAt<MiddlewareB>(PipelineStage.Observe, owner: "xihan.a");
        var original = builder.Dump();

        // 一组只有一个归属：混进别的归属会被错撤或漏撤
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => builder.UseOwned(Plugin, static p => p
            .UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .UseNamedInstance("q:first", new MiddlewareA([]), PipelineStage.Observe, owner: "xihan.plugins.q"))).Code);
        // 约束只能挂在组内的中间件上：挂在组外的不会随这一组撤回
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => builder.UseOwned(Plugin, static p => p
            .UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .After(typeof(MiddlewareB).FullName!, "p:first"))).Code);
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => builder.UseOwned(Plugin, static p => p
            .UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .UseOwned(Plugin, static _ => { }))).Code);
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => builder.UseOwned(Plugin, static p => p.RemoveOwnedBy("xihan.a"))).Code);

        Assert.Equal(original, builder.Dump());
    }

    [Fact]
    public void RemoveOwnedBy_ThatWouldLeaveADanglingConstraint_IsRejectedAndChangesNothing()
    {
        // 别的归属的中间件以次序约束引用了插件的中间件：撤回会让它指向不存在的中间件
        var builder = new PipelineBuilder()
            .UseNamedInstance("p:first", new MiddlewareA([]), PipelineStage.Observe, owner: Plugin)
            .UseAt<MiddlewareB>(PipelineStage.Observe, owner: "xihan.a")
            .After(typeof(MiddlewareB).FullName!, "p:first");
        var original = builder.Dump();

        var exception = Assert.Throws<FrameworkException>(() => builder.RemoveOwnedBy(Plugin));

        Assert.Equal(KernelErrors.PipelineRegistration, exception.Code);
        Assert.Contains("p:first", exception.Message, StringComparison.Ordinal);
        Assert.Equal(original, builder.Dump());
    }

    [Fact]
    public async Task RemoveOwnedBy_AppliesToCallsStartedAfterIt_WhileACallInFlightFinishesThroughTheWithdrawnMiddleware()
    {
        var log = new List<string>();
        PipelineBuilder pipeline = null!;
        var (provider, app) = Hosts.BuildApp(b =>
        {
            pipeline = b.Pipeline;
            b.UseFeature(new EchoFeature());
            b.Pipeline.UseInstance(new TracingMiddleware(log, "P"), PipelineStage.Observe, owner: Plugin);
        });
        using var _ = provider;
        await app.StartAsync(CancellationToken.None);

        // 第一个调用在处理器里停住：它进入管道时取到的委托里有 P
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = InvokeAsync(provider, app, async _ =>
        {
            entered.SetResult();
            await release.Task;
        });
        await entered.Task;
        Assert.Equal(["enter:P"], log);

        Assert.Equal(1, pipeline.RemoveOwnedBy(Plugin));

        // 撤回之后开始的调用不再经过 P
        var handled = false;
        Assert.True((await InvokeAsync(provider, app, _ =>
        {
            handled = true;
            return ValueTask.CompletedTask;
        })).IsSuccess);
        Assert.True(handled);
        Assert.Equal(["enter:P"], log);
        Assert.DoesNotContain(nameof(TracingMiddleware), app.DescribePipeline(), StringComparison.Ordinal);

        // 在途的调用按它取到的委托走完，P 的后半段照样执行
        release.SetResult();
        Assert.True((await inFlight).IsSuccess);
        Assert.Equal(["enter:P", "exit:P"], log);

        // 再成组登记回来：之后的调用又经过它
        pipeline.UseOwned(Plugin, p => p.UseInstance(new TracingMiddleware(log, "Q"), PipelineStage.Observe, owner: Plugin));
        Assert.True((await InvokeAsync(provider, app, static _ => ValueTask.CompletedTask)).IsSuccess);
        Assert.Equal(["enter:P", "exit:P", "enter:Q", "exit:Q"], log);
    }

    [Fact]
    public void BuiltApplication_AcceptsOnlyGroupedChangesOfInstances()
    {
        PipelineBuilder pipeline = null!;
        var (provider, app) = Hosts.BuildApp(b =>
        {
            pipeline = b.Pipeline;
            b.UseFeature(new EchoFeature());
        });
        using var _ = provider;
        var original = app.DescribePipeline();

        // 单项改动不核对就生效，构建之后会让下一次调用在构建管道时才失败
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => pipeline.UseInstance(new MiddlewareA([]), PipelineStage.Observe)).Code);
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => pipeline.After(typeof(OperationCaptureMiddleware).FullName!, "xihan.other:missing")).Code);
        // 容器已经建好：按类型登记的中间件进不了容器
        var byType = Assert.Throws<FrameworkException>(() => pipeline.UseOwned(Plugin, static p => p.UseAt<MiddlewareC>(PipelineStage.Observe, owner: Plugin)));
        Assert.Equal(KernelErrors.PipelineRegistration, byType.Code);
        Assert.Contains("UseNamedInstance", byType.Message, StringComparison.Ordinal);
        Assert.Equal(original, app.DescribePipeline());

        pipeline.UseOwned(Plugin, static p => p.UseInstance(new MiddlewareC([]), PipelineStage.Observe, owner: Plugin));
        Assert.Contains(typeof(MiddlewareC).FullName!, app.DescribePipeline(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 在新作用域里经应用的管道执行一次处理器。
    /// </summary>
    private static async Task<Result> InvokeAsync(IServiceProvider provider, App app, PipelineStep handler)
    {
        await using var scope = provider.CreateAsyncScope();
        return await app.InvokeAsync(Hosts.Context(scope.ServiceProvider), handler);
    }
}
