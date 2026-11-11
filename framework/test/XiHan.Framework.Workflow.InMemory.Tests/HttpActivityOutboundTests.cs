// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// <c>Http</c> 活动的目标地址过出站地址策略（RFC-0008、RFC-0021、ADR-0117）
/// </summary>
/// <remarks>
/// 流程定义由租户管理员编辑，地址不可信：用真实的监听端口，断言缺省连接根本没有到达，放行之后才到达
/// </remarks>
public sealed class HttpActivityOutboundTests
{
    [Fact]
    public async Task LoopbackTarget_IsDeniedByDefault_AndNothingReachesIt()
    {
        using var server = new OneResponseServer();

        var instance = await RunAsync(server.Url);

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        var fault = Assert.IsType<WorkflowFault>(instance.Fault);
        Assert.Equal(WorkflowErrors.ActivityFailed, fault.Code);
        Assert.Equal(WorkflowMessages.HttpAddressDenied, fault.MessageKey);
        Assert.Equal("127.0.0.1", fault.Arguments?["host"]);
        Assert.Equal(0, server.Accepted);
    }

    [Fact]
    public async Task LoopbackTarget_IsReached_OnceTheConfigurationAllowsIt()
    {
        using var server = new OneResponseServer();

        var instance = await RunAsync(server.Url, new KeyValuePair<string, string?>("XiHan:Security:Outbound:AllowLoopback", "true"));

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(1, server.Accepted);
    }

    /// <summary>
    /// 跑一个向 <paramref name="url"/> 发请求的工作流
    /// </summary>
    private static async Task<WorkflowInstance> RunAsync(string url, params KeyValuePair<string, string?>[] configuration)
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"), .. configuration]);
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("outbound"))
            .AddStart()
            .AddNode("call", new ActivityType(ActivityTypes.Http), configure: node => node.WithProperty(HttpActivity.UrlProperty, url))
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
        return (await run.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = new WorkflowName("outbound") }, CancellationToken.None)).ValueOrThrow();
    }

    /// <summary>
    /// 在回环地址上监听、对每个连接答一次 200 的服务
    /// </summary>
    private sealed class OneResponseServer : IDisposable
    {
        /// <summary>
        /// 监听器
        /// </summary>
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        /// <summary>
        /// 接受到的连接数
        /// </summary>
        private int _accepted;

        /// <summary>
        /// 开始监听
        /// </summary>
        public OneResponseServer()
        {
            _listener.Start();
            _ = ServeAsync();
        }

        /// <summary>
        /// 接受到的连接数
        /// </summary>
        public int Accepted => Volatile.Read(ref _accepted);

        /// <summary>
        /// 服务的地址
        /// </summary>
        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";

        /// <inheritdoc />
        public void Dispose() => _listener.Dispose();

        /// <summary>
        /// 接受连接并作答，直到监听器释放
        /// </summary>
        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    Interlocked.Increment(ref _accepted);
                    var stream = client.GetStream();
                    var buffer = new byte[4096];
                    _ = await stream.ReadAsync(buffer);
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
                }
            }
            catch (ObjectDisposedException)
            {
                // 监听器释放：结束
            }
            catch (SocketException)
            {
                // 监听器释放：结束
            }
        }
    }
}
