// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 工作流的事件带上按 <c>type</c> 登记的数据模式
/// </summary>
/// <remarks>
/// RFC-0021「实例与执行」的事件与 RFC-0009「框架事件的数据模式」（ADR-0323）：修之前引擎与 <c>PublishEvent</c> 活动发布的信封都不带 <c>dataschema</c>
/// </remarks>
public sealed class WorkflowEventDataSchemaTests
{
    /// <summary>
    /// 登记给 <c>workflow.instance.started</c> 的数据模式
    /// </summary>
    private const string StartedSchema = "https://example.com/schemas/workflow-event.json";

    /// <summary>
    /// 登记给 <c>workflow.custom</c> 的数据模式
    /// </summary>
    private const string CustomSchema = "urn:example:workflow-custom";

    [Fact]
    public async Task EngineAndPublishEventActivity_CarryTheDataSchemaRegisteredForTheType_AndOthersDoNot()
    {
        var publisher = new RecordingPublisher();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")],
            services =>
            {
                services.AddSingleton<IMessagePublisher>(publisher);
                services.AddMessageDataSchema(WorkflowTopics.InstanceStarted, StartedSchema);
                services.AddMessageDataSchema(WorkflowTopics.Custom, CustomSchema);
            });
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("announces"))
            .AddStart()
            .AddNode("announce", new ActivityType(ActivityTypes.PublishEvent), configure: static node => node.WithProperty(PublishEventActivity.EventNameProperty, "order.approved"))
            .AddEnd()
            .AddTransition("start", "announce").AddTransition("announce", "end")
            .Build();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
            var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
            (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
        }

        await using (var scope = host.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = new WorkflowName("announces") }, CancellationToken.None)).ValueOrThrow();
        }

        var started = await publisher.WaitAsync(WorkflowTopics.InstanceStarted, TimeSpan.FromSeconds(10));
        var custom = await publisher.WaitAsync(WorkflowTopics.Custom, TimeSpan.FromSeconds(10));
        var completed = await publisher.WaitAsync(WorkflowTopics.InstanceCompleted, TimeSpan.FromSeconds(10));
        Assert.Equal((WorkflowTopics.Source, StartedSchema), (started.Source, started.DataSchema));
        Assert.Equal(("order.approved", CustomSchema), (custom.Subject, custom.DataSchema));
        Assert.Null(completed.DataSchema);
    }
}
