// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 内存工作流特性（<c>role: default</c>）：提供 <c>workflow.execution@1</c>——有界内存存储、进程内实例锁、引擎，应用进入服务状态后轮询到期书签。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowFeature : IHostedFeature
{
    /// <summary>
    /// 启动后保存的轮询器，供停止时使用。
    /// </summary>
    private DueBookmarkPoller? _poller;

    /// <summary>
    /// 轮询选项。
    /// </summary>
    private InMemoryWorkflowOptions? _options;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.workflow.inmemory",
        Package = "XiHan.Framework.Workflow.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(WorkflowCapabilities.Execution, WorkflowCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution(WorkflowOptions.ConfigurationSection) },
        Entry = typeof(InMemoryWorkflowFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(WorkflowMessages.Catalog);
        _options = InMemoryWorkflowOptions.FromConfiguration(context.Configuration);
        var engineOptions = WorkflowOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(_options);
        context.Services.AddSingleton<InMemoryWorkflowDefinitionStore>();
        context.Services.AddSingleton<InMemoryWorkflowInstanceStore>();
        context.Services.AddSingleton<InMemoryWorkflowBookmarkStore>();
        context.Services.AddSingleton<InMemoryWorkflowInstanceLock>();
        if (!context.Node.IsSelectedFor(WorkflowCapabilities.Execution))
        {
            return;
        }

        // 被选中才把契约指向本包的实现并装配引擎；被显式 Provider 顶替时只留本包自己的类型
        context.Services.TryAddSingleton<IWorkflowDefinitionStore>(static sp => sp.GetRequiredService<InMemoryWorkflowDefinitionStore>());
        context.Services.TryAddSingleton<IWorkflowInstanceStore>(static sp => sp.GetRequiredService<InMemoryWorkflowInstanceStore>());
        context.Services.TryAddSingleton<IWorkflowBookmarkStore>(static sp => sp.GetRequiredService<InMemoryWorkflowBookmarkStore>());
        context.Services.TryAddSingleton<IWorkflowInstanceLock>(static sp => sp.GetRequiredService<InMemoryWorkflowInstanceLock>());
        context.Services.AddWorkflowEngine(engineOptions);
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _poller = services.GetService<DueBookmarkPoller>();
        return _poller is null || _options is null ? Task.CompletedTask : _poller.StartAsync(_options.PollInterval, _options.BatchSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_poller is not null)
        {
            await _poller.StopAsync(cancellationToken).ConfigureAwait(false);
            _poller = null;
        }
    }
}
