// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 实例锁。
/// </summary>
/// <remarks>
/// 同一实例同一时刻只有一个执行者。不可重入；等待超过 <see cref="WorkflowOptions.LockTimeout"/> → <see cref="WorkflowErrors.LockTimeout"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowInstanceLock
{
    /// <summary>
    /// 获取。
    /// </summary>
    /// <remarks>
    /// 释放返回值即解锁。
    /// </remarks>
    ValueTask<Result<IAsyncDisposable>> AcquireAsync(string instanceId, CancellationToken cancellationToken);
}
