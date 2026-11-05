// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 装了发件箱的宿主上，数据库工作流特性对 SQLite 照样通过用户任务与外部任务套件
/// </summary>
/// <remarks>
/// 在 <see cref="SqliteDatabase"/> 的装配上再装内存消息与 <see cref="OutboxFeature"/>：引擎的事件随业务事务写进发件箱表，提交后才投递到代理，回滚的不投递（ADR-0096 第六节）
/// </remarks>
public sealed class DatabaseWorkflowTaskConformanceWithOutboxTests : WorkflowTaskConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2"),
        };
        if (configuration is not null)
        {
            settings.AddRange(configuration);
        }

        var database = new SqliteDatabase();
        var host = await database.StartHostAsync(settings, configure, static b => b.UseFeature<InMemoryMessagingFeature>().UseFeature<OutboxFeature>());
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), database);
    }
}
