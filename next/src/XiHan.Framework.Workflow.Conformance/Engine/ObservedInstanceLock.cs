// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 把实例锁请求报告给探针的锁装饰器。
/// </summary>
/// <remarks>
/// 报告之后原样转给 Provider 的锁，等待与超时语义不变。
/// </remarks>
internal sealed class ObservedInstanceLock(IWorkflowInstanceLock inner, StartRaceProbe probe) : IWorkflowInstanceLock
{
    /// <inheritdoc />
    public ValueTask<Result<IAsyncDisposable>> AcquireAsync(string instanceId, CancellationToken cancellationToken)
    {
        probe.OnLockRequested();
        return inner.AcquireAsync(instanceId, cancellationToken);
    }
}
