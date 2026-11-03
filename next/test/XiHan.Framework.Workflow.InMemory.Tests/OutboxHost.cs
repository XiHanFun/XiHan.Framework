// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 装了发件箱的内存工作流宿主
/// </summary>
/// <remarks>
/// 内存持久化、内存消息与 <see cref="OutboxFeature"/> 一起装配：引擎的事件写进发件箱、提交后才投递到代理（ADR-0096 第六节）。发件箱的投递循环照缺省周期运行，与套件主动投递并存
/// </remarks>
internal static class OutboxHost
{
    /// <summary>
    /// 启动宿主并包成引擎套件的夹具
    /// </summary>
    public static async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
            new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2"),
        };
        if (configuration is not null)
        {
            settings.AddRange(configuration);
        }

        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryMessagingFeature>().UseFeature<OutboxFeature>().UseFeature<InMemoryWorkflowFeature>(), settings, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), host);
    }
}
