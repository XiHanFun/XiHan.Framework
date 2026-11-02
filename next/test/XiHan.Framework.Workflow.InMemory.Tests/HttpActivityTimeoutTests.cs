// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// HTTP 活动的超时与取消归因。
/// </summary>
/// <remarks>
/// RFC-0021「HTTP 活动的超时与取消」，ADR-0097。活动按引擎时钟给请求计时，归因只看两个令牌：活动自己的超时令牌已触发即超时，不论调用方是否也已取消、客户端是抛出还是交回了响应；只有调用方取消时按取消处理（抛出 <see cref="OperationCanceledException"/>）。
/// 探针活动从引擎拿到真实的活动上下文，再以用例控制的调用方令牌执行 HTTP 活动；时钟是注入的假时钟、客户端在用例放行之前一直挂着，两个令牌谁先触发完全由用例决定，不靠真实等待。
/// </remarks>
public sealed class HttpActivityTimeoutTests
{
    /// <summary>
    /// 探针节点上 HTTP 活动的超时。
    /// </summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 两个令牌怎样触发。
    /// </summary>
    public enum Trigger
    {
        /// <summary>
        /// 都不触发。
        /// </summary>
        None,

        /// <summary>
        /// 只有活动的超时。
        /// </summary>
        Timeout,

        /// <summary>
        /// 只有调用方取消。
        /// </summary>
        Caller,

        /// <summary>
        /// 先超时、再取消。
        /// </summary>
        TimeoutThenCaller,

        /// <summary>
        /// 先取消、再超时。
        /// </summary>
        CallerThenTimeout,
    }

    /// <summary>
    /// 客户端被放行后怎样结束。
    /// </summary>
    public enum Reply
    {
        /// <summary>
        /// 抛出取消（令牌都没触发时就是客户端自己的超时）。
        /// </summary>
        Throw,

        /// <summary>
        /// 接住取消、照常交回一个响应。
        /// </summary>
        Respond,
    }

    [Theory]
    [InlineData(Trigger.Timeout, Reply.Throw)]
    [InlineData(Trigger.Timeout, Reply.Respond)]
    [InlineData(Trigger.TimeoutThenCaller, Reply.Throw)]
    [InlineData(Trigger.TimeoutThenCaller, Reply.Respond)]
    [InlineData(Trigger.CallerThenTimeout, Reply.Throw)]
    [InlineData(Trigger.CallerThenTimeout, Reply.Respond)]
    [InlineData(Trigger.None, Reply.Throw)]
    public async Task TimeoutTokenFired_OrTheClientsOwnTimeout_IsReportedAsTimeout(Trigger trigger, Reply reply)
    {
        var outcome = await RunAsync(trigger, reply);

        var error = Assert.IsType<Result<ActivityOutcome>>(outcome).Error;
        Assert.NotNull(error);
        Assert.Equal(WorkflowErrors.ActivityFailed, error.Code);
        Assert.Equal(("timeout", 0), ((string?)error.Details!["reason"], (int?)error.Details["status"]));
    }

    [Theory]
    [InlineData(Reply.Throw)]
    [InlineData(Reply.Respond)]
    public async Task OnlyTheCallerCancelled_IsCancellation(Reply reply)
    {
        var outcome = await RunAsync(Trigger.Caller, reply);

        Assert.IsAssignableFrom<OperationCanceledException>(outcome);
    }

    [Fact]
    public async Task ClientTimeoutException_IsReportedAsTimeout()
    {
        var outcome = await RunAsync(Trigger.None, Reply.Throw, static () => new TimeoutException("The client gave up."));

        var error = Assert.IsType<Result<ActivityOutcome>>(outcome).Error;
        Assert.Equal(("timeout", 0), ((string?)error!.Details!["reason"], (int?)error.Details["status"]));
    }

    [Fact]
    public async Task NeitherTokenFired_TheResponseIsTheOutcome()
    {
        var outcome = await RunAsync(Trigger.None, Reply.Respond);

        var completed = Assert.IsType<Result<ActivityOutcome>>(outcome).ValueOrThrow();
        Assert.Equal(200, completed.Outputs![HttpActivity.DefaultResultVariable]!["status"]!.GetValue<int>());
    }

    /// <summary>
    /// 按给定的触发与放行方式执行一次 HTTP 活动。
    /// </summary>
    /// <remarks>
    /// 起一个以假时钟与挂起的客户端装配的内存工作流，按 <paramref name="trigger"/> 触发令牌、按 <paramref name="reply"/> 放行客户端。返回 HTTP 活动的结果（<see cref="Result{T}"/>）或它抛出的异常。<paramref name="failure"/> 给出时客户端抛出它，代替取消。
    /// </remarks>
    private static async Task<object> RunAsync(Trigger trigger, Reply reply, Func<Exception>? failure = null)
    {
        var clock = new FakeTimeProvider();
        var probe = new ProbeState();
        var handler = new GatedHandler(failure);
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")],
            services =>
            {
                // 时钟在特性装配之前登记：引擎交给活动的时钟就是它
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton(probe);
                services.AddSingleton(new WorkflowHttpClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }));
                services.AddActivity<ProbeActivity>(ProbeActivity.TypeName);
            });
        var definition = WorkflowDefinitionBuilder.Create("http-attribution")
            .AddStart()
            .AddNode("call", ProbeActivity.TypeName, configure: node => node
                .WithProperty(HttpActivity.UrlProperty, "https://api.test/orders")
                .WithProperty(HttpActivity.TimeoutSecondsProperty, _timeout.TotalSeconds))
            .AddEnd()
            .AddTransition("start", "call").AddTransition("call", "end")
            .Build();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
            var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
            (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
        }

        await using var run = host.Services.CreateAsyncScope();
        var started = run.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = "http-attribution" }, CancellationToken.None);

        // 请求已交给客户端、两个令牌都还没触发：从这里起由用例决定先后
        await handler.Entered.Task;
        switch (trigger)
        {
            case Trigger.Timeout:
                clock.Advance(_timeout);
                break;
            case Trigger.Caller:
                await probe.Caller.CancelAsync();
                break;
            case Trigger.TimeoutThenCaller:
                clock.Advance(_timeout);
                await probe.Caller.CancelAsync();
                break;
            case Trigger.CallerThenTimeout:
                await probe.Caller.CancelAsync();
                clock.Advance(_timeout);
                break;
        }

        handler.Release.SetResult(reply);
        var outcome = await probe.Outcome.Task;
        (await started).ValueOrThrow();
        return outcome;
    }

    /// <summary>
    /// 探针与用例共享的状态。
    /// </summary>
    private sealed class ProbeState
    {
        /// <summary>
        /// 用例控制的调用方令牌。
        /// </summary>
        public CancellationTokenSource Caller { get; } = new();

        /// <summary>
        /// HTTP 活动的结果或抛出的异常。
        /// </summary>
        public TaskCompletionSource<object> Outcome { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 以用例的调用方令牌执行 HTTP 活动的探针。
    /// </summary>
    /// <remarks>
    /// 节点属性就是 HTTP 活动的属性；记下活动的结果或异常后照常完成，引擎的推进与本归因无关。
    /// </remarks>
    private sealed class ProbeActivity(ProbeState state) : IActivity
    {
        /// <summary>
        /// 活动类型名。
        /// </summary>
        public const string TypeName = "HttpProbe";

        /// <inheritdoc />
        public async ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
        {
            try
            {
                state.Outcome.SetResult(await new HttpActivity().ExecuteAsync(context, state.Caller.Token));
            }
            catch (Exception exception)
            {
                state.Outcome.SetResult(exception);
            }

            return ActivityOutcome.Complete();
        }
    }

    /// <summary>
    /// 收到请求后挂起、由用例放行的处理器。
    /// </summary>
    /// <remarks>
    /// 不看令牌等待：令牌在挂起期间触发，放行之后才按 <see cref="Reply"/> 结束，模拟传输在两个令牌都触发之后才报告。
    /// </remarks>
    private sealed class GatedHandler(Func<Exception>? failure) : HttpMessageHandler
    {
        /// <summary>
        /// 请求已到。
        /// </summary>
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 放行与结束方式。
        /// </summary>
        public TaskCompletionSource<Reply> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Entered.SetResult();
            if (await Release.Task.ConfigureAwait(false) == Reply.Throw)
            {
                // 令牌都没触发时抛出的取消就是客户端自己的超时（如应用替换的 HttpClient 设了 Timeout）
                throw failure?.Invoke() ?? new OperationCanceledException(cancellationToken);
            }

            // 先缓冲内容：令牌已触发时 HttpClient 读响应体就会抛出取消，缓冲过的内容不再读，响应原样交回活动
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json") };
            await response.Content.LoadIntoBufferAsync(CancellationToken.None).ConfigureAwait(false);
            return response;
        }
    }
}
