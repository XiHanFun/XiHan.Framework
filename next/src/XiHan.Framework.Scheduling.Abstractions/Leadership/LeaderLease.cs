// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Leadership;

/// <summary>
/// 领导租约。
/// </summary>
/// <remarks>
/// 持有期间本实例是资源的领导；到期前须 <see cref="RenewAsync"/>，不再需要时 <see cref="ReleaseAsync"/>。释放即放弃。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class LeaderLease : IAsyncDisposable
{
    /// <summary>
    /// 创建。
    /// </summary>
    protected LeaderLease(JobName resource, string holder)
    {
        Resource = resource;
        Holder = holder ?? throw new ArgumentNullException(nameof(holder));
    }

    /// <summary>
    /// 资源名。
    /// </summary>
    public JobName Resource { get; }

    /// <summary>
    /// 持有者标识：实例的身份（进程内实现每次获取生成新值，Kubernetes 实现是 Pod 名）。
    /// </summary>
    public string Holder { get; }

    /// <summary>
    /// 到期时刻。
    /// </summary>
    public abstract DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// 是否仍持有（未过期、未释放、未被他人取得）。
    /// </summary>
    public abstract bool IsHeld { get; }

    /// <summary>
    /// 续约一个存活时间。
    /// </summary>
    /// <remarks>
    /// 已过期或已释放返回 <see cref="SchedulingErrors.LeaseHeld"/>。
    /// </remarks>
    public abstract ValueTask<Result> RenewAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 释放。
    /// </summary>
    /// <remarks>
    /// 幂等。
    /// </remarks>
    public abstract ValueTask ReleaseAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
