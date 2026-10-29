// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Resilience;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Resilience;

/// <summary>
/// Resilience 阶段的内置中间件。
/// </summary>
/// <remarks>
/// 管道里紧随截止时间中间件；固定窗口限流、并发上限与熔断按配置拒绝并不进 Handle；按操作覆盖；调用方错误不算熔断失败；关闭内置中间件与非操作调用不受限。
/// </remarks>
public sealed class ResilienceMiddlewareTests
{
    /// <summary>
    /// 空容器。
    /// </summary>
    /// <remarks>
    /// 直接调用中间件的用例用它构造上下文。
    /// </remarks>
    private static readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// 构建并启动装配了回声特性与 Handle 阶段门闸的应用。
    /// </summary>
    /// <remarks>
    /// <paramref name="clock"/> 给定时注册为时间源。
    /// </remarks>
    private static async Task<(ServiceProvider Provider, App App, GateMiddleware Gate)> StartAsync(FakeTimeProvider? clock = null, params (string Key, string Value)[] settings)
    {
        var gate = new GateMiddleware();
        var (provider, app) = Hosts.BuildApp(
            b =>
            {
                if (clock is not null)
                {
                    b.Services.AddSingleton<TimeProvider>(clock);
                }

                b.UseFeature(new EchoFeature());
                b.Pipeline.UseInstance(gate, PipelineStage.Handle, Placement.Beginning);
            },
            Hosts.Configuration(settings));
        await app.StartAsync(CancellationToken.None);
        return (provider, app, gate);
    }

    /// <summary>
    /// 在新作用域里调用一次回声操作。
    /// </summary>
    private static async Task<OperationResponse> InvokeAsync(ServiceProvider provider, string text = "hi")
    {
        await using var scope = provider.CreateAsyncScope();
        var payload = Encoding.UTF8.GetBytes($$"""{"text":"{{text}}"}""");
        return await provider.GetRequiredService<IOperationInvoker>().InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, payload));
    }

    /// <summary>
    /// 让下游以不可用结果结束而不进处理器。
    /// </summary>
    private static ValueTask FailUnavailable(InvocationContext context, PipelineStep next)
    {
        context.SetOutcome(Error.Unavailable("ECHOES_UNAVAILABLE_001", "echoes.down"));
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void BuiltInMiddleware_PlacesResilienceRightAfterDeadline()
    {
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new EchoFeature()));
        using var _ = provider;

        var dump = app.DescribePipeline();
        var deadline = dump.IndexOf("resilience   XiHan.Framework.Kernel.Pipeline.DeadlineMiddleware", StringComparison.Ordinal);
        var resilience = dump.IndexOf("resilience   XiHan.Framework.Kernel.Resilience.ResilienceMiddleware", StringComparison.Ordinal);
        Assert.True(deadline >= 0 && resilience > deadline, dump);
        Assert.True(provider.GetRequiredService<ResilienceOptions>().Defaults.IsEmpty);
    }

    [Fact]
    public async Task NoLimits_PassesThrough()
    {
        var (provider, app, gate) = await StartAsync();
        await using var _ = provider;

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await InvokeAsync(provider)).IsSuccess);
        }

        Assert.Equal(3, gate.Calls);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RateLimit_RejectsBeyondPermits_WithRetryAfter()
    {
        var (provider, app, gate) = await StartAsync(settings: [("XiHan:Kernel:Resilience:PermitLimit", "2"), ("XiHan:Kernel:Resilience:WindowSeconds", "60")]);
        await using var _ = provider;

        Assert.True((await InvokeAsync(provider)).IsSuccess);
        Assert.True((await InvokeAsync(provider)).IsSuccess);
        var rejected = await InvokeAsync(provider);

        Assert.Equal(KernelErrors.RateLimited, rejected.Error?.Code);
        Assert.Equal(EchoOperation.Id, rejected.Error!.Details!["id"]);
        Assert.InRange((int)rejected.Error.Details["retryAfter"]!, 1, 60);
        // 被拒绝的调用没进 Handle
        Assert.Equal(2, gate.Calls);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Concurrency_RejectsSecondCall_WhileFirstIsRunning()
    {
        var (provider, app, gate) = await StartAsync(settings: [("XiHan:Kernel:Resilience:ConcurrencyLimit", "1")]);
        await using var _ = provider;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.Behavior = async (context, next) =>
        {
            entered.TrySetResult();
            await release.Task;
            await next(context);
        };

        var first = InvokeAsync(provider);
        await entered.Task;
        var second = await InvokeAsync(provider);
        Assert.Equal(KernelErrors.ConcurrencyExceeded, second.Error?.Code);
        Assert.Equal(EchoOperation.Id, second.Error!.Details!["id"]);
        Assert.Equal(1, gate.Calls);

        // 第一个结束后许可释放，再来的调用正常
        release.SetResult();
        Assert.True((await first).IsSuccess);
        gate.Behavior = static (context, next) => next(context);
        Assert.True((await InvokeAsync(provider)).IsSuccess);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterConsecutiveInfrastructureFailures_AndRecoversAfterBreak()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        var (provider, app, gate) = await StartAsync(clock, ("XiHan:Kernel:Resilience:FailureThreshold", "2"), ("XiHan:Kernel:Resilience:BreakSeconds", "30"));
        await using var _ = provider;
        gate.Behavior = FailUnavailable;

        Assert.Equal("ECHOES_UNAVAILABLE_001", (await InvokeAsync(provider)).Error?.Code);
        Assert.Equal("ECHOES_UNAVAILABLE_001", (await InvokeAsync(provider)).Error?.Code);
        var rejected = await InvokeAsync(provider);
        Assert.Equal(KernelErrors.CircuitOpen, rejected.Error?.Code);
        Assert.Equal(30, rejected.Error!.Details!["retryAfter"]);
        Assert.Equal(2, gate.Calls);

        // 未到期仍拒绝；到期后放一次试探，成功即关闭
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(KernelErrors.CircuitOpen, (await InvokeAsync(provider)).Error?.Code);
        clock.Advance(TimeSpan.FromSeconds(1));
        gate.Behavior = static (context, next) => next(context);
        Assert.True((await InvokeAsync(provider)).IsSuccess);
        Assert.True((await InvokeAsync(provider)).IsSuccess);
        Assert.Equal(4, gate.Calls);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CircuitBreaker_FailedProbe_ReopensForAnotherBreak()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        var (provider, app, gate) = await StartAsync(clock, ("XiHan:Kernel:Resilience:FailureThreshold", "1"), ("XiHan:Kernel:Resilience:BreakSeconds", "10"));
        await using var _ = provider;
        gate.Behavior = FailUnavailable;

        Assert.Equal("ECHOES_UNAVAILABLE_001", (await InvokeAsync(provider)).Error?.Code);
        clock.Advance(TimeSpan.FromSeconds(10));
        // 试探失败：重新打开一个周期
        Assert.Equal("ECHOES_UNAVAILABLE_001", (await InvokeAsync(provider)).Error?.Code);
        var rejected = await InvokeAsync(provider);
        Assert.Equal(KernelErrors.CircuitOpen, rejected.Error?.Code);
        Assert.Equal(10, rejected.Error!.Details!["retryAfter"]);

        clock.Advance(TimeSpan.FromSeconds(10));
        gate.Behavior = static (context, next) => next(context);
        Assert.True((await InvokeAsync(provider)).IsSuccess);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CircuitBreaker_HalfOpen_AdmitsSingleProbe()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        var (provider, app, gate) = await StartAsync(clock, ("XiHan:Kernel:Resilience:FailureThreshold", "1"), ("XiHan:Kernel:Resilience:BreakSeconds", "10"));
        await using var _ = provider;
        gate.Behavior = FailUnavailable;
        Assert.Equal("ECHOES_UNAVAILABLE_001", (await InvokeAsync(provider)).Error?.Code);
        clock.Advance(TimeSpan.FromSeconds(10));

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.Behavior = async (context, next) =>
        {
            entered.TrySetResult();
            await release.Task;
            await next(context);
        };

        // 试探在途时其余调用仍被拒绝
        var probe = InvokeAsync(provider);
        await entered.Task;
        var during = await InvokeAsync(provider);
        Assert.Equal(KernelErrors.CircuitOpen, during.Error?.Code);
        Assert.Equal(10, during.Error!.Details!["retryAfter"]);

        release.SetResult();
        Assert.True((await probe).IsSuccess);
        gate.Behavior = static (context, next) => next(context);
        Assert.True((await InvokeAsync(provider)).IsSuccess);
        // 进过 Handle 的只有首次失败、试探与试探成功后的调用；试探在途时被拒绝的没进
        Assert.Equal(3, gate.Calls);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CircuitBreaker_CountsUnhandledExceptions()
    {
        var clock = new FakeTimeProvider();
        var (provider, app, gate) = await StartAsync(clock, ("XiHan:Kernel:Resilience:FailureThreshold", "1"));
        await using var _ = provider;

        // 处理器抛异常：异常穿过本中间件记失败，再由 Observe 阶段翻译成 UNEXPECTED
        Assert.Equal(KernelErrors.Unexpected, (await InvokeAsync(provider, "throw")).Error?.Code);
        Assert.Equal(KernelErrors.CircuitOpen, (await InvokeAsync(provider)).Error?.Code);
        Assert.Equal(1, gate.Calls);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CircuitBreaker_IgnoresCallerErrors()
    {
        var clock = new FakeTimeProvider();
        var (provider, app, gate) = await StartAsync(clock, ("XiHan:Kernel:Resilience:FailureThreshold", "1"));
        await using var _ = provider;

        // 空文本是操作返回的校验失败：调用方的问题，不算下游坏了
        Assert.Equal("ECHOES_VALIDATION_001", (await InvokeAsync(provider, "")).Error?.Code);
        Assert.Equal("ECHOES_VALIDATION_001", (await InvokeAsync(provider, "")).Error?.Code);
        Assert.True((await InvokeAsync(provider)).IsSuccess);
        Assert.Equal(3, gate.Calls);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OperationOverride_WinsOverDefaults()
    {
        var (provider, app, gate) = await StartAsync(settings:
        [
            ("XiHan:Kernel:Resilience:PermitLimit", "1"),
            ("XiHan:Kernel:Resilience:WindowSeconds", "60"),
            ($"XiHan:Kernel:Resilience:Operations:{EchoOperation.Id}:PermitLimit", "3"),
        ]);
        await using var _ = provider;

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await InvokeAsync(provider)).IsSuccess);
        }

        Assert.Equal(KernelErrors.RateLimited, (await InvokeAsync(provider)).Error?.Code);
        Assert.Equal(3, gate.Calls);
        // 其他操作沿用默认策略，覆盖未写的键也沿用默认
        var options = provider.GetRequiredService<ResilienceOptions>();
        Assert.Equal(1, options.PolicyFor("others.create").PermitLimit);
        Assert.Equal(TimeSpan.FromSeconds(60), options.PolicyFor(EchoOperation.Id).Window);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BuiltInMiddlewareDisabled_AppliesNoLimits()
    {
        var (provider, app, gate) = await StartAsync(settings: [("XiHan:Kernel:UseBuiltInMiddleware", "false"), ("XiHan:Kernel:Resilience:PermitLimit", "1")]);
        await using var _ = provider;

        Assert.DoesNotContain("ResilienceMiddleware", app.DescribePipeline(), StringComparison.Ordinal);
        Assert.Null(provider.GetService<ResilienceOptions>());
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await InvokeAsync(provider)).IsSuccess);
        }

        Assert.Equal(3, gate.Calls);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task InvocationWithoutOperation_IsNotLimited()
    {
        var options = new ResilienceOptions { Defaults = new ResiliencePolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1), ConcurrencyLimit = 1, FailureThreshold = 1 } };
        using var middleware = new ResilienceMiddleware(options, TimeProvider.System);

        for (var i = 0; i < 3; i++)
        {
            var context = App.CreateContext(_services, CancellationToken.None);
            var called = false;
            await middleware.InvokeAsync(context, _ =>
            {
                called = true;
                return ValueTask.CompletedTask;
            });

            Assert.True(called);
            Assert.False(context.GetOutcome().HasValue);
        }
    }
}
