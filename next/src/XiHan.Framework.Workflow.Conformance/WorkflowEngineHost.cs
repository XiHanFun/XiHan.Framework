// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Conformance;

/// <summary>
/// 引擎套件的夹具：装配了被测 Provider 的宿主，以及"轮询一次到期书签"的入口；引擎、目录、任务服务都在新作用域里解析。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowEngineHost : IAsyncDisposable
{
    /// <summary>
    /// 释放句柄。
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 轮询一次。
    /// </summary>
    private readonly Func<CancellationToken, ValueTask<int>> _poll;

    /// <summary>
    /// 创建。
    /// </summary>
    public WorkflowEngineHost(IServiceProvider services, Func<CancellationToken, ValueTask<int>> poll, IAsyncDisposable host)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        _poll = poll ?? throw new ArgumentNullException(nameof(poll));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 根服务提供者。
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 轮询一次到期书签；返回恢复的书签数。
    /// </summary>
    public ValueTask<int> PollAsync(CancellationToken cancellationToken = default) => _poll(cancellationToken);

    /// <summary>
    /// 在新作用域里执行。
    /// </summary>
    public async ValueTask<T> InScopeAsync<T>(Func<IServiceProvider, ValueTask<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider).ConfigureAwait(false);
    }

    /// <summary>
    /// 创建并发布定义，返回发布后的定义。
    /// </summary>
    public ValueTask<WorkflowDefinition> PublishAsync(WorkflowDefinition definition) => InScopeAsync(async sp =>
    {
        var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
        var created = (await catalog.CreateAsync(definition, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        return (await catalog.PublishAsync(created.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
    });

    /// <summary>
    /// 启动实例。
    /// </summary>
    public ValueTask<Result<WorkflowInstance>> StartAsync(WorkflowName name, JsonObject? variables = null, string? starterId = null, string? correlationId = null, string? tenantId = null)
        => InScopeAsync(sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = name, Variables = variables, StarterId = starterId, CorrelationId = correlationId, TenantId = tenantId }, CancellationToken.None));

    /// <summary>
    /// 读实例（存储里的当前状态）。
    /// </summary>
    public async ValueTask<WorkflowInstance> InstanceAsync(string id)
        => (await Services.GetRequiredService<IWorkflowInstanceStore>().FindAsync(id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();

    /// <summary>
    /// 读实例的节点实例。
    /// </summary>
    public async ValueTask<IReadOnlyList<WorkflowNodeInstance>> NodeInstancesAsync(string instanceId)
        => (await Services.GetRequiredService<IWorkflowInstanceStore>().NodeInstancesAsync(instanceId, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();

    /// <summary>
    /// 读实例的书签。
    /// </summary>
    public async ValueTask<IReadOnlyList<WorkflowBookmark>> BookmarksAsync(string instanceId)
        => (await Services.GetRequiredService<IWorkflowBookmarkStore>().ByInstanceAsync(instanceId, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();

    /// <summary>
    /// 某人的待办。
    /// </summary>
    public ValueTask<IReadOnlyList<UserTask>> PendingAsync(string assigneeId)
        => InScopeAsync(async sp => (await sp.GetRequiredService<IWorkflowTaskService>().PendingAsync(assigneeId, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

    /// <summary>
    /// 完成任务。
    /// </summary>
    public ValueTask<Result<WorkflowInstance>> CompleteAsync(string taskId, string actorId, string outcome, JsonObject? variables = null, string? comment = null)
        => InScopeAsync(sp => sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(taskId, actorId, outcome, comment, variables), CancellationToken.None));

    /// <summary>
    /// 转办任务。
    /// </summary>
    public ValueTask<Result> TransferAsync(string taskId, string actorId, string target)
        => InScopeAsync(sp => sp.GetRequiredService<IWorkflowTaskService>().TransferAsync(new UserTaskTransfer(taskId, actorId, target), CancellationToken.None));

    /// <summary>
    /// 加签。
    /// </summary>
    public ValueTask<Result> AddAssigneesAsync(string taskId, string actorId, params IReadOnlyList<string> assignees)
        => InScopeAsync(sp => sp.GetRequiredService<IWorkflowTaskService>().AddAssigneesAsync(new UserTaskAddition(taskId, actorId, assignees), CancellationToken.None));

    /// <summary>
    /// 发信号。
    /// </summary>
    public ValueTask<Result<int>> SignalAsync(string name, JsonObject? payload = null, string? correlationId = null)
        => InScopeAsync(sp => sp.GetRequiredService<IWorkflowEngine>().SignalAsync(name, payload, correlationId, CancellationToken.None));

    /// <summary>
    /// 对引擎执行一个操作。
    /// </summary>
    public ValueTask<T> EngineAsync<T>(Func<IWorkflowEngine, ValueTask<T>> action)
        => InScopeAsync(sp => action(sp.GetRequiredService<IWorkflowEngine>()));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
