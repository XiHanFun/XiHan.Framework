// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using Xunit;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 引擎套件的夹具。
/// </summary>
/// <remarks>
/// 装配了被测 Provider 的宿主，以及"轮询一次到期书签"的入口。两种用法：<see cref="InScopeAsync{T}"/> 在没有调用的新作用域里解析服务（引擎自己开调用，平台分区），<see cref="AsTenantAsync{T}"/> 在以给定租户开的一次调用里解析（加入这次调用的事务）。读存储的辅助方法都在调用里、按给定分区读。
/// </remarks>
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
    /// 轮询一次到期书签。
    /// </summary>
    /// <remarks>
    /// 返回恢复的书签数。
    /// </remarks>
    public ValueTask<int> PollAsync(CancellationToken cancellationToken = default) => _poll(cancellationToken);

    /// <summary>
    /// 在没有调用的新作用域里执行：引擎、目录与任务服务自己开调用（平台分区）。
    /// </summary>
    public async ValueTask<T> InScopeAsync<T>(Func<IServiceProvider, ValueTask<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider).ConfigureAwait(false);
    }

    /// <summary>
    /// 以给定租户（空为平台）在一次经管道的调用里执行，处理器返回后调用提交。
    /// </summary>
    /// <remarks>
    /// 租户预先写进调用的安全上下文，Transaction 阶段按它开工作单元；宿主不必装配多租户。断言失败不交给管道翻译成意外错误：记下来在调用之外原样抛出。<paramref name="rollback"/> 为真时处理器之后让调用以失败结束（业务回滚）；<paramref name="readOnly"/> 为真时调用带一个动词为 <c>get</c> 的操作描述，Transaction 阶段开只读工作单元。
    /// </remarks>
    public async ValueTask<T> AsTenantAsync<T>(string? tenantId, Func<IServiceProvider, ValueTask<T>> action, bool rollback = false, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        if (tenantId is not null)
        {
            context.SetTenant(new TenantContext(tenantId));
        }

        if (readOnly)
        {
            context.SetOperation(new ReadOnlyProbeOperation());
        }

        var outcome = default(T);
        ExceptionDispatchInfo? failure = null;
        var invoked = await Services.GetRequiredService<App>().InvokeAsync(context, async invocation =>
        {
            try
            {
                outcome = await action(invocation.Services).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            if (rollback && failure is null)
            {
                // 业务在流程操作之后失败：抛出让 Transaction 阶段回滚这次调用里的全部写入
                throw new InvalidOperationException("The business operation failed after the workflow operation; the invocation rolls back.");
            }
        }).ConfigureAwait(false);
        failure?.Throw();
        Assert.Equal(rollback, invoked.IsFailure);
        return outcome!;
    }

    /// <summary>
    /// 创建并发布定义，返回发布后的定义。
    /// </summary>
    public async ValueTask<WorkflowDefinition> PublishAsync(WorkflowDefinition definition, string? tenantId = null)
        => (await PublishManyAsync(tenantId, definition).ConfigureAwait(false))[0];

    /// <summary>
    /// 在一个分区里批量创建并发布定义，按入参顺序返回发布后的定义。
    /// </summary>
    /// <remarks>
    /// 以该分区的租户开一次调用：逐个创建，再一次批量发布。
    /// </remarks>
    public ValueTask<IReadOnlyList<WorkflowDefinition>> PublishManyAsync(string? tenantId, params IReadOnlyList<WorkflowDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return AsTenantAsync<IReadOnlyList<WorkflowDefinition>>(tenantId, async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            var ids = new List<string>(definitions.Count);
            foreach (var definition in definitions)
            {
                ids.Add((await catalog.CreateAsync(definition, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Id);
            }

            return [.. (await catalog.PublishAsync(ids, CancellationToken.None).ConfigureAwait(false)).Select(static published => published.ValueOrThrow())];
        });
    }

    /// <summary>
    /// 启动实例。
    /// </summary>
    /// <remarks>
    /// 没给租户时在没有调用的作用域里启动（引擎自己开调用，平台分区），给了租户时以该租户开一次调用启动。
    /// </remarks>
    public ValueTask<Result<WorkflowInstance>> StartAsync(WorkflowName name, JsonObject? variables = null, string? starterId = null, string? correlationId = null, string? tenantId = null)
    {
        var request = new WorkflowStartRequest { Name = name, Variables = variables, StarterId = starterId, CorrelationId = correlationId };
        return tenantId is null
            ? InScopeAsync(sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(request, CancellationToken.None))
            : AsTenantAsync(tenantId, sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(request, CancellationToken.None));
    }

    /// <summary>
    /// 读实例（存储里已提交的当前状态）。
    /// </summary>
    public ValueTask<WorkflowInstance> InstanceAsync(string id, string? tenantId = null)
        => AsTenantAsync(tenantId, async sp => (await sp.GetRequiredService<IWorkflowInstanceStore>().FindAsync(tenantId, id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow());

    /// <summary>
    /// 实例在不在存储里。
    /// </summary>
    public ValueTask<bool> InstanceExistsAsync(string id, string? tenantId = null)
        => AsTenantAsync(tenantId, async sp => (await sp.GetRequiredService<IWorkflowInstanceStore>().FindAsync(tenantId, id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);

    /// <summary>
    /// 读实例的节点实例。
    /// </summary>
    public ValueTask<IReadOnlyList<WorkflowNodeInstance>> NodeInstancesAsync(string instanceId, string? tenantId = null)
        => AsTenantAsync(tenantId, async sp => (await sp.GetRequiredService<IWorkflowInstanceStore>().NodeInstancesAsync(tenantId, instanceId, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

    /// <summary>
    /// 读实例的书签。
    /// </summary>
    public ValueTask<IReadOnlyList<WorkflowBookmark>> BookmarksAsync(string instanceId, string? tenantId = null)
        => AsTenantAsync(tenantId, async sp => (await sp.GetRequiredService<IWorkflowBookmarkStore>().ByInstanceAsync(tenantId, instanceId, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

    /// <summary>
    /// 按条件列实例。
    /// </summary>
    public ValueTask<IReadOnlyList<WorkflowInstance>> ListInstancesAsync(WorkflowInstanceQuery query, string? tenantId = null)
        => AsTenantAsync(tenantId, async sp => (await sp.GetRequiredService<IWorkflowInstanceStore>().ListAsync(tenantId, query, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

    /// <summary>
    /// 某人的待办。
    /// </summary>
    public ValueTask<IReadOnlyList<UserTask>> PendingAsync(string assigneeId, string? tenantId = null)
        => AsTenantAsync(tenantId, async sp => (await sp.GetRequiredService<IWorkflowTaskService>().PendingAsync(assigneeId, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

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
