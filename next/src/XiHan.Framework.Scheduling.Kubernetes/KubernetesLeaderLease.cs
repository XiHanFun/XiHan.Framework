// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Kubernetes.Leases;

namespace XiHan.Framework.Scheduling.Kubernetes;

/// <summary>
/// 以 Lease 对象承载的租约。
/// </summary>
/// <remarks>
/// 续约写 <c>renewTime</c>，释放清空 <c>holderIdentity</c>；每次写都带 resourceVersion，版本过期即视为已被他人接管。
/// </remarks>
internal sealed class KubernetesLeaderLease : LeaderLease
{
    /// <summary>
    /// 客户端。
    /// </summary>
    private readonly KubernetesLeaseClient _client;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 存活时间。
    /// </summary>
    private readonly TimeSpan _timeToLive;

    /// <summary>
    /// 最近一次写入后的 Lease 对象（含 resourceVersion）。
    /// </summary>
    private LeaseDocument _document;

    /// <summary>
    /// 是否已释放或已失去。
    /// </summary>
    private bool _lost;

    /// <summary>
    /// 创建。
    /// </summary>
    public KubernetesLeaderLease(KubernetesLeaseClient client, TimeProvider clock, JobName resource, string holder, TimeSpan timeToLive, LeaseDocument document)
        : base(resource, holder)
    {
        _client = client;
        _clock = clock;
        _timeToLive = timeToLive;
        _document = document;
    }

    /// <inheritdoc />
    public override DateTimeOffset ExpiresAt => (MicroTime.Parse(_document.Spec.RenewTime) ?? DateTimeOffset.MinValue) + _timeToLive;

    /// <inheritdoc />
    public override bool IsHeld => !_lost && _clock.GetUtcNow() < ExpiresAt;

    /// <inheritdoc />
    public override async ValueTask<Result> RenewAsync(CancellationToken cancellationToken)
    {
        if (_lost || !IsHeld)
        {
            return Held();
        }

        // 带着上次写入的 resourceVersion 续约：期间被他人接管会因版本过期得到 409
        var renewed = Clone(_document);
        renewed.Spec.RenewTime = MicroTime.Format(_clock.GetUtcNow());
        var result = await _client.ReplaceAsync(renewed, cancellationToken).ConfigureAwait(false);
        if (!result.TryGetValue(out var written))
        {
            if (string.Equals(result.Error!.Code, SchedulingErrors.LeaseHeld, StringComparison.Ordinal))
            {
                _lost = true;
            }

            return result.Error;
        }

        _document = written;
        return Result.Success();
    }

    /// <inheritdoc />
    public override async ValueTask ReleaseAsync()
    {
        if (_lost)
        {
            return;
        }

        _lost = true;
        // 清空持有者让他人立即可取；失败（已被接管、不可达）不影响释放语义
        var released = Clone(_document);
        released.Spec.HolderIdentity = string.Empty;
        released.Spec.RenewTime = MicroTime.Format(_clock.GetUtcNow());
        await _client.ReplaceAsync(released, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 被占用。
    /// </summary>
    private Error Held() => Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", Resource.Value)));

    /// <summary>
    /// 复制一份文档用于写入。
    /// </summary>
    internal static LeaseDocument Clone(LeaseDocument source) => new()
    {
        ApiVersion = source.ApiVersion,
        Kind = source.Kind,
        Metadata = new LeaseMetadata { Name = source.Metadata.Name, Namespace = source.Metadata.Namespace, ResourceVersion = source.Metadata.ResourceVersion },
        Spec = new LeaseSpec
        {
            HolderIdentity = source.Spec.HolderIdentity,
            LeaseDurationSeconds = source.Spec.LeaseDurationSeconds,
            AcquireTime = source.Spec.AcquireTime,
            RenewTime = source.Spec.RenewTime,
            LeaseTransitions = source.Spec.LeaseTransitions,
        },
    };
}
