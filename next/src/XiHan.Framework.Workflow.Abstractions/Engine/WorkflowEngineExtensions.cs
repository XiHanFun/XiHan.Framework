// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 单实例的控制操作：批量方法的特例。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowEngineExtensions
{
    /// <summary>
    /// 挂起一个实例。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> SuspendAsync(this IWorkflowEngine engine, string instanceId, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.SuspendAsync([instanceId], reason, cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 恢复一个挂起的实例。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> ResumeInstanceAsync(this IWorkflowEngine engine, string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.ResumeInstancesAsync([instanceId], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 取消一个实例。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> CancelAsync(this IWorkflowEngine engine, string instanceId, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.CancelAsync([instanceId], reason, cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 终止一个实例。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> TerminateAsync(this IWorkflowEngine engine, string instanceId, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.TerminateAsync([instanceId], reason, cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 重试一个故障实例。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> RetryAsync(this IWorkflowEngine engine, string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.RetryAsync([instanceId], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 改一个实例的变量。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> SetVariablesAsync(this IWorkflowEngine engine, WorkflowVariableUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.SetVariablesAsync([update], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 跳转一个实例。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> JumpAsync(this IWorkflowEngine engine, WorkflowJump jump, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.JumpAsync([jump], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 迁移一个实例。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> MigrateAsync(this IWorkflowEngine engine, WorkflowMigration migration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return (await engine.MigrateAsync([migration], cancellationToken).ConfigureAwait(false))[0];
    }
}
