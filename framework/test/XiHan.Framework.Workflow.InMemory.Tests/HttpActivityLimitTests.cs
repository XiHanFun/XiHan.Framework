// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// <c>Http</c> 活动的响应体与超时上限（ADR-0181）
/// </summary>
/// <remarks>
/// 流程定义由租户用户编辑，地址指向的服务器不可信：响应体读到上限即停，不先整体缓冲；超时不超过引擎选项 <c>MaxHttpTimeoutSeconds</c>，不是正的秒数即属性错误
/// </remarks>
public sealed class HttpActivityLimitTests
{
    /// <summary>
    /// 活动保留的响应体字节数
    /// </summary>
    private const int BodyLimit = 4 * 1024 * 1024;

    [Fact]
    public async Task OversizedResponse_IsReadOnlyUpToTheLimit()
    {
        var body = new EndlessStream(64 * 1024 * 1024);
        var handler = new RespondingHandler(() => new StreamContent(body) { Headers = { ContentType = new("text/plain") } });

        var instance = await RunHttpAsync(handler, static _ => { });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        var result = instance.Variables[HttpActivity.DefaultResultVariable]!;
        Assert.True(result["truncated"]!.GetValue<bool>());
        Assert.Equal(BodyLimit, result["body"]!.GetValue<string>().Length);
        // 读到上限多一个字节就停，最多再多一个读缓冲；修之前整体缓冲，64 MiB 全部读完
        Assert.InRange(body.Served, BodyLimit + 1, BodyLimit + (1024 * 1024));
    }

    [Fact]
    public async Task BodyThatStallsAfterTheHeaders_IsBoundedByTheActivityTimeout()
    {
        var clock = new FakeTimeProvider();
        var body = new StallingStream();
        var handler = new RespondingHandler(() => new StreamContent(body) { Headers = { ContentType = new("text/plain") } });

        var running = RunHttpAsync(handler, static node => node.WithProperty(HttpActivity.TimeoutSecondsProperty, 5), clock);
        await body.Reading.Task;
        clock.Advance(TimeSpan.FromSeconds(5));
        var instance = await running;

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal(WorkflowMessages.HttpTimedOut, instance.Fault?.MessageKey);
    }

    [Theory]
    [InlineData(301d)]
    [InlineData(86_400_000d)]
    public async Task TimeoutOverTheEngineLimit_IsRejectedWhenTheDefinitionIsPublished(double seconds)
    {
        var errors = await PublishAsync(node => node.WithProperty(HttpActivity.TimeoutSecondsProperty, seconds));

        var issue = Assert.Single(errors);
        Assert.Equal("$.nodes[1].properties.TimeoutSeconds", issue.Path);
        Assert.Equal(WorkflowMessages.PropertySecondsOverLimit, issue.MessageKey);
        Assert.Equal(300d, Convert.ToDouble(issue.Arguments?["max"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("\"soon\"")]
    [InlineData("1e300")]
    public async Task TimeoutThatIsNotAPositiveNumber_IsRejectedWhenTheDefinitionIsPublished(string json)
    {
        var errors = await PublishAsync(node => node.WithProperty(HttpActivity.TimeoutSecondsProperty, JsonNode.Parse(json)));

        var issue = Assert.Single(errors);
        Assert.Equal("$.nodes[1].properties.TimeoutSeconds", issue.Path);
        Assert.Equal(WorkflowMessages.PropertyNotSeconds, issue.MessageKey);
    }

    [Fact]
    public async Task TimeoutWithinTheConfiguredLimit_IsAccepted()
    {
        var errors = await PublishAsync(node => node.WithProperty(HttpActivity.TimeoutSecondsProperty, 600), new KeyValuePair<string, string?>($"{WorkflowOptions.ConfigurationSection}:MaxHttpTimeoutSeconds", "900"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task TimeoutOverTheLimit_InADefinitionThatSkippedValidation_FaultsTheNodeWithoutSendingIt()
    {
        // 校验之后宿主才调低上限、或定义早于本条限制已经发布：活动自己再核对一次
        var handler = new RespondingHandler(static () => new StringContent("ok"));

        var instance = await RunProbeAsync(handler, static node => node.WithProperty(HttpActivity.TimeoutSecondsProperty, 86_400 * 100));

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal(WorkflowErrors.InputInvalid, instance.Fault?.Code);
        Assert.Equal(WorkflowMessages.PropertySecondsOverLimit, instance.Fault?.MessageKey);
        Assert.Equal(0, handler.Sent);
    }

    /// <summary>
    /// 跑一个只有一个 <c>Http</c> 节点的工作流
    /// </summary>
    private static Task<WorkflowInstance> RunHttpAsync(HttpMessageHandler handler, Action<WorkflowNodeBuilder> configure, TimeProvider? clock = null)
        => RunAsync(handler, ActivityTypes.Http, configure, clock);

    /// <summary>
    /// 跑一个由探针转调 <c>Http</c> 活动的工作流
    /// </summary>
    /// <remarks>
    /// 探针节点不按 <c>Http</c> 的属性校验，模拟没经过校验的定义
    /// </remarks>
    private static Task<WorkflowInstance> RunProbeAsync(HttpMessageHandler handler, Action<WorkflowNodeBuilder> configure)
        => RunAsync(handler, UncheckedHttpActivity.TypeName, configure, null);

    /// <summary>
    /// 跑一个只有一个活动节点的工作流，返回结束时的实例
    /// </summary>
    private static async Task<WorkflowInstance> RunAsync(HttpMessageHandler handler, string activityType, Action<WorkflowNodeBuilder> configure, TimeProvider? clock)
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")],
            services =>
            {
                if (clock is not null)
                {
                    services.AddSingleton(clock);
                }

                services.AddSingleton(new WorkflowHttpClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }));
                services.AddActivity<UncheckedHttpActivity>(new ActivityType(UncheckedHttpActivity.TypeName));
            });
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("http-limits"))
            .AddStart()
            .AddNode("call", new ActivityType(activityType), configure: node => configure(node.WithProperty(HttpActivity.UrlProperty, "https://api.test/orders")))
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
        return (await run.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = new WorkflowName("http-limits") }, CancellationToken.None)).ValueOrThrow();
    }

    /// <summary>
    /// 发布一个 <c>Http</c> 节点按 <paramref name="configure"/> 配置的定义，返回校验问题（发布成功为空）
    /// </summary>
    private static async Task<IReadOnlyList<SchemaViolation>> PublishAsync(Action<WorkflowNodeBuilder> configure, params KeyValuePair<string, string?>[] configuration)
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"), .. configuration]);
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("http-timeout"))
            .AddStart()
            .AddNode("call", new ActivityType(ActivityTypes.Http), configure: node => configure(node.WithProperty(HttpActivity.UrlProperty, "https://api.test/orders")))
            .AddEnd()
            .AddTransition("start", "call").AddTransition("call", "end")
            .Build();
        await using var scope = host.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
        var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
        var published = await catalog.PublishAsync(created.Id, CancellationToken.None);
        return published.IsSuccess ? [] : Assert.IsAssignableFrom<IReadOnlyList<SchemaViolation>>(published.Error!.Details!["errors"]);
    }

    /// <summary>
    /// 节点属性按 <c>Http</c> 活动执行、但不按它校验的活动
    /// </summary>
    private sealed class UncheckedHttpActivity : IActivity
    {
        /// <summary>
        /// 活动类型名
        /// </summary>
        public const string TypeName = "UncheckedHttp";

        /// <inheritdoc />
        public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
            => new HttpActivity().ExecuteAsync(context, cancellationToken);
    }

    /// <summary>
    /// 对每个请求答 200、正文由工厂给出的处理器
    /// </summary>
    private sealed class RespondingHandler(Func<HttpContent> content) : HttpMessageHandler
    {
        /// <summary>
        /// 收到的请求数
        /// </summary>
        private int _sent;

        /// <summary>
        /// 收到的请求数
        /// </summary>
        public int Sent => Volatile.Read(ref _sent);

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sent);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content() });
        }
    }

    /// <summary>
    /// 给出 <c>length</c> 个字节 <c>a</c> 的只读流，记下已经交出去多少
    /// </summary>
    private sealed class EndlessStream(long length) : Stream
    {
        /// <summary>
        /// 已交出的字节数
        /// </summary>
        private long _served;

        /// <summary>
        /// 已交出的字节数
        /// </summary>
        public long Served => Interlocked.Read(ref _served);

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            var served = Interlocked.Read(ref _served);
            var take = (int)Math.Min(count, length - served);
            buffer.AsSpan(offset, take).Fill((byte)'a');
            Interlocked.Add(ref _served, take);
            return take;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// 第一次读就挂起、直到令牌触发的只读流
    /// </summary>
    private sealed class StallingStream : Stream
    {
        /// <summary>
        /// 已经开始读
        /// </summary>
        public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reading.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
