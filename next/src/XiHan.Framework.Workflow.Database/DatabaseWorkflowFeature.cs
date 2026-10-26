// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Entities;
using XiHan.Framework.Workflow.Database.Locking;
using XiHan.Framework.Workflow.Database.Polling;
using XiHan.Framework.Workflow.Database.Storage;

namespace XiHan.Framework.Workflow.Database;

/// <summary>
/// 数据库工作流特性（<c>role: provider</c>）：提供 <c>workflow.execution@1</c>，依赖 <c>persistence.relational@1</c>——定义、实例、节点实例与书签经仓储读写，实例锁与轮询领导权以租约行实现，应用进入服务状态后由持有领导权的节点轮询到期书签。装配后顶替默认的内存工作流。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowFeature : IHostedFeature
{
    /// <summary>
    /// 启动后保存的轮询循环，供停止时使用。
    /// </summary>
    private DueBookmarkPollingLoop? _loop;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.workflow.database",
        Package = "XiHan.Framework.Workflow.Database",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(WorkflowCapabilities.Execution, WorkflowCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Config = new ConfigContribution(WorkflowOptions.ConfigurationSection),
            Health = ["workflow.database.poller"],
        },
        Entry = typeof(DatabaseWorkflowFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(WorkflowMessages.Catalog);
        var options = DatabaseWorkflowOptions.FromConfiguration(context.Configuration);
        var engineOptions = WorkflowOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        // 实例锁的等待上限来自引擎选项；几个 Provider 读的是同一份配置，谁先注册都一样
        context.Services.TryAddSingleton(engineOptions);
        context.Services.AddRepository<WorkflowDefinitionRecord, string>();
        context.Services.AddRepository<WorkflowInstanceRecord, string>();
        context.Services.AddRepository<WorkflowNodeInstanceRecord, string>();
        context.Services.AddRepository<WorkflowBookmarkRecord, string>();
        context.Services.AddRepository<WorkflowLockRecord, string>();
        context.Services.AddSingleton<DatabaseWorkflowDefinitionStore>();
        context.Services.AddSingleton<DatabaseWorkflowInstanceStore>();
        context.Services.AddSingleton<DatabaseWorkflowBookmarkStore>();
        context.Services.AddSingleton<DatabaseWorkflowInstanceLock>();
        if (!context.Node.IsSelectedFor(WorkflowCapabilities.Execution))
        {
            return;
        }

        // 被选中才把契约指向本包的实现并装配引擎与轮询；被别的显式 Provider 顶替时只留本包自己的类型
        context.Services.TryAddSingleton<IWorkflowDefinitionStore>(static sp => sp.GetRequiredService<DatabaseWorkflowDefinitionStore>());
        context.Services.TryAddSingleton<IWorkflowInstanceStore>(static sp => sp.GetRequiredService<DatabaseWorkflowInstanceStore>());
        context.Services.TryAddSingleton<IWorkflowBookmarkStore>(static sp => sp.GetRequiredService<DatabaseWorkflowBookmarkStore>());
        context.Services.TryAddSingleton<IWorkflowInstanceLock>(static sp => sp.GetRequiredService<DatabaseWorkflowInstanceLock>());
        context.Services.AddWorkflowEngine(engineOptions);
        context.Services.AddSingleton<PollerLeadership>();
        context.Services.AddSingleton<DueBookmarkPollingLoop>();
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _loop = services.GetService<DueBookmarkPollingLoop>();
        return _loop is null ? Task.CompletedTask : _loop.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null)
        {
            await _loop.StopAsync(cancellationToken).ConfigureAwait(false);
            _loop = null;
        }
    }
}
