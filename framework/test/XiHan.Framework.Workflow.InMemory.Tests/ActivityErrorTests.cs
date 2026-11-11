// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 活动失败时的故障（RFC-0021、ADR-0115）
/// </summary>
/// <remarks>
/// 故障存进实例、经接口返回：活动抛出与 HTTP 连接失败时，<c>reason</c> 只写异常类型，完整异常进日志
/// </remarks>
public sealed class ActivityErrorTests
{
    /// <summary>
    /// 异常消息里带着不该外露的地址
    /// </summary>
    private const string Secret = "erp.internal:8443";

    [Fact]
    public async Task ActivityThatThrows_FaultsWithOnlyTheExceptionType_AndLogsTheFullException()
    {
        var logs = new LogCapture();
        var fault = await RunAsync(logs, ThrowingActivity.TypeName, static services => services.AddActivity<ThrowingActivity>(new ActivityType(ThrowingActivity.TypeName)));

        Assert.Equal(WorkflowErrors.ActivityFailed, fault.Code);
        Assert.Equal(typeof(InvalidOperationException).FullName, fault.Arguments?["reason"]);
        Assert.DoesNotContain(Secret, fault.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(Assert.Single(logs.Exceptions, static entry => entry.Exception.Message.Contains(Secret, StringComparison.Ordinal)).Exception);
    }

    [Fact]
    public async Task HttpConnectionFailure_FaultsWithOnlyTheExceptionType_AndLogsTheFullException()
    {
        var logs = new LogCapture();
        var fault = await RunAsync(logs, ActivityTypes.Http, static services => services.AddSingleton(new WorkflowHttpClient(new HttpClient(new RefusingHandler()))));

        Assert.Equal(WorkflowErrors.ActivityFailed, fault.Code);
        Assert.Equal(typeof(HttpRequestException).FullName, fault.Arguments?["reason"]);
        Assert.DoesNotContain(Secret, fault.Message, StringComparison.Ordinal);
        var logged = Assert.Single(logs.Exceptions, static entry => entry.Category == typeof(HttpActivity).FullName);
        Assert.Contains(Secret, logged.Exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 跑一个只有一个活动节点的工作流，返回实例的故障
    /// </summary>
    private static async Task<WorkflowFault> RunAsync(LogCapture logs, string activityType, Action<IServiceCollection> configure)
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")],
            services =>
            {
                services.AddSingleton<ILoggerFactory>(logs);
                configure(services);
            });
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("activity-errors"))
            .AddStart()
            .AddNode("call", new ActivityType(activityType), configure: static node => node.WithProperty(HttpActivity.UrlProperty, "https://erp.test/orders"))
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
        var instance = (await run.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = new WorkflowName("activity-errors") }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        return Assert.IsType<WorkflowFault>(instance.Fault);
    }

    /// <summary>
    /// 总是抛出的活动
    /// </summary>
    private sealed class ThrowingActivity : IActivity
    {
        /// <summary>
        /// 活动类型名
        /// </summary>
        public const string TypeName = "test.throwing";

        /// <inheritdoc />
        public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"POST https://{Secret}/orders failed.");
    }

    /// <summary>
    /// 总是连不上的处理器
    /// </summary>
    private sealed class RefusingHandler : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException($"Connection refused ({Secret}).");
    }
}
