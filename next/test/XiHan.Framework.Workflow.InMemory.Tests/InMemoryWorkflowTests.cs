// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Tasks;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 内存实现自己的行为：容量上限、选项读取、特性清单与轮询器的启停。
/// </summary>
public sealed class InMemoryWorkflowTests
{
    [Fact]
    public async Task Stores_RejectWritesBeyondCapacity_AsAWhole()
    {
        var options = new InMemoryWorkflowOptions { MaxDefinitions = 2, MaxInstances = 1, MaxBookmarks = 1 };
        var definitions = new InMemoryWorkflowDefinitionStore(options);
        var d1 = Definition("cap-a");
        var d2 = Definition("cap-b");
        var d3 = Definition("cap-c");

        // 整批超限一条不写；替换已有的不占新容量
        var rejected = await definitions.SaveAsync([d1, d2, d3], CancellationToken.None);
        Assert.Equal(WorkflowErrors.StoreCapacity, rejected.Error?.Code);
        Assert.Equal(0, definitions.Count);
        Assert.True((await definitions.SaveAsync([d1, d2], CancellationToken.None)).IsSuccess);
        Assert.True((await definitions.SaveAsync([d1 with { DisplayName = "again" }], CancellationToken.None)).IsSuccess);
        Assert.Equal(WorkflowErrors.StoreCapacity, (await definitions.SaveAsync([d3], CancellationToken.None)).Error?.Code);

        var instances = new InMemoryWorkflowInstanceStore(options);
        Assert.True((await instances.SaveAsync([Instance("i1")], CancellationToken.None)).IsSuccess);
        Assert.Equal(WorkflowErrors.StoreCapacity, (await instances.SaveAsync([Instance("i2")], CancellationToken.None)).Error?.Code);

        var bookmarks = new InMemoryWorkflowBookmarkStore(options);
        Assert.True((await bookmarks.SaveAsync([Bookmark("i1")], CancellationToken.None)).IsSuccess);
        Assert.Equal(WorkflowErrors.StoreCapacity, (await bookmarks.SaveAsync([Bookmark("i1")], CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public void Options_ReadFromConfiguration_AndValidate()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{InMemoryWorkflowOptions.ConfigurationSection}:MaxDefinitions"] = "5",
            [$"{InMemoryWorkflowOptions.ConfigurationSection}:MaxInstances"] = "6",
            [$"{InMemoryWorkflowOptions.ConfigurationSection}:MaxNodeInstances"] = "7",
            [$"{InMemoryWorkflowOptions.ConfigurationSection}:MaxBookmarks"] = "8",
            [$"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds"] = "0.5",
            [$"{InMemoryWorkflowOptions.ConfigurationSection}:BatchSize"] = "9",
            [$"{WorkflowOptions.ConfigurationSection}:MaxNodeExecutionsPerBurst"] = "10",
            [$"{WorkflowOptions.ConfigurationSection}:MaxSubWorkflowDepth"] = "3",
            [$"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds"] = "1.5",
            [$"{WorkflowOptions.ConfigurationSection}:NotResumableBackoffSeconds"] = "60",
            [$"{WorkflowOptions.ConfigurationSection}:ExpressionCacheSize"] = "11",
        }).Build();

        var options = InMemoryWorkflowOptions.FromConfiguration(configuration);
        Assert.Equal((5, 6, 7, 8, TimeSpan.FromMilliseconds(500), 9), (options.MaxDefinitions, options.MaxInstances, options.MaxNodeInstances, options.MaxBookmarks, options.PollInterval, options.BatchSize));
        var engine = WorkflowOptions.FromConfiguration(configuration);
        Assert.Equal((10, 3, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(60), 11), (engine.MaxNodeExecutionsPerBurst, engine.MaxSubWorkflowDepth, engine.LockTimeout, engine.NotResumableBackoff, engine.ExpressionCacheSize));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryWorkflowOptions { MaxInstances = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowOptions { LockTimeout = TimeSpan.Zero });
    }

    [Fact]
    public async Task Feature_IsDefaultProvider_AndRunsThePoller()
    {
        var feature = new InMemoryWorkflowFeature();
        Assert.Equal((ManifestRole.Default, ManifestKind.Provider), (feature.Manifest.Role, feature.Manifest.Kind));
        Assert.Equal([WorkflowCapabilities.Execution], feature.Manifest.Provides.Select(static capability => capability.Id));

        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryWorkflowFeature>(), [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "0.05")]);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        Assert.True(poller.IsRunning);
        Assert.Same(host.Services.GetRequiredService<InMemoryWorkflowDefinitionStore>(), host.Services.GetRequiredService<IWorkflowDefinitionStore>());
        Assert.Same(host.Services.GetRequiredService<InMemoryWorkflowInstanceLock>(), host.Services.GetRequiredService<IWorkflowInstanceLock>());
        await using (var scope = host.Services.CreateAsyncScope())
        {
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowEngine>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowTaskService>());
        }

        // 到期的延时经后台轮询恢复，不用手工 Poll
        var definition = WorkflowDefinitionBuilder.Create("poll-delay")
            .AddStart().AddDelay("wait", TimeSpan.FromMilliseconds(20)).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end")
            .Build();
        string instanceId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
            var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
            (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
            instanceId = (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = "poll-delay" }, CancellationToken.None)).ValueOrThrow().Id;
        }

        var store = host.Services.GetRequiredService<IWorkflowInstanceStore>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while ((await store.FindAsync(instanceId, CancellationToken.None)).ValueOrThrow().ValueOrThrow().Status != WorkflowInstanceStatus.Completed)
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The poller did not resume the timer bookmark.");
            await Task.Delay(25);
        }

        await host.App.StopAsync(CancellationToken.None);
        Assert.False(poller.IsRunning);
    }

    /// <summary>
    /// 一个定义。
    /// </summary>
    private static WorkflowDefinition Definition(string name) => new()
    {
        Id = WorkflowIdentifiers.NewId(),
        Name = new WorkflowName(name),
        Nodes = [new WorkflowNode { Id = "start", ActivityType = ActivityTypes.Start }],
    };

    /// <summary>
    /// 一个实例。
    /// </summary>
    private static WorkflowInstance Instance(string id) => new() { Id = id, DefinitionId = "d", DefinitionName = new WorkflowName("cap") };

    /// <summary>
    /// 一个书签。
    /// </summary>
    private static WorkflowBookmark Bookmark(string instanceId) => new() { Id = WorkflowIdentifiers.NewId(), InstanceId = instanceId, NodeId = "n", NodeInstanceId = "ni", Kind = BookmarkKinds.Timer };
}
