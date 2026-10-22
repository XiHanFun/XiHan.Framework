// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Kubernetes;

/// <summary>
/// 以 Kubernetes Lease 实现的领导选举：Lease 不存在就创建，持有者为空或 <c>renewTime + leaseDurationSeconds</c> 已过就接管（交接次数加一），否则返回被占用；并发竞争由 resourceVersion 裁决。
/// 存活时间按秒向上取整写进 Lease，本地判定仍用精确值。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class KubernetesLeaderElection : ILeaderElection
{
    /// <summary>
    /// 客户端。
    /// </summary>
    private readonly KubernetesLeaseClient _client;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly KubernetesSchedulingOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 本实例的持有者标识。
    /// </summary>
    private readonly string _holder;

    /// <summary>
    /// 创建。
    /// </summary>
    public KubernetesLeaderElection(KubernetesLeaseClient client, KubernetesSchedulingOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<KubernetesLeaderElection>();
        _holder = options.ResolveHolder();
    }

    /// <summary>
    /// 本实例的持有者标识。
    /// </summary>
    public string Holder => _holder;

    /// <inheritdoc />
    public async ValueTask<Result<LeaderLease>> TryAcquireAsync(JobName resource, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "The time to live must be positive.");
        }

        var name = _options.LeaseNameFor(resource);
        var now = _clock.GetUtcNow();
        var seconds = (int)Math.Ceiling(timeToLive.TotalSeconds);
        var existing = await _client.GetAsync(name, cancellationToken).ConfigureAwait(false);
        if (!existing.TryGetValue(out var maybe))
        {
            return existing.Error!;
        }

        Result<LeaseDocument> written;
        if (!maybe.TryGetValue(out var current))
        {
            // 不存在：创建就是获取；并发创建只有一个成功，其余 409
            written = await _client.CreateAsync(new LeaseDocument
            {
                Metadata = new LeaseMetadata { Name = name, Namespace = _client.Namespace },
                Spec = new LeaseSpec { HolderIdentity = _holder, LeaseDurationSeconds = seconds, AcquireTime = MicroTime.Format(now), RenewTime = MicroTime.Format(now), LeaseTransitions = 0 },
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // 有持有者且未过期（含自己：不可重入）→ 被占用；否则带版本接管
            var renewedAt = MicroTime.Parse(current.Spec.RenewTime);
            var expired = string.IsNullOrEmpty(current.Spec.HolderIdentity)
                || renewedAt is null
                || renewedAt.Value + TimeSpan.FromSeconds(current.Spec.LeaseDurationSeconds ?? 0) <= now;
            if (!expired)
            {
                return Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", resource.Value)));
            }

            var takeover = KubernetesLeaderLease.Clone(current);
            takeover.Spec.HolderIdentity = _holder;
            takeover.Spec.LeaseDurationSeconds = seconds;
            takeover.Spec.AcquireTime = MicroTime.Format(now);
            takeover.Spec.RenewTime = MicroTime.Format(now);
            takeover.Spec.LeaseTransitions = (current.Spec.LeaseTransitions ?? 0) + 1;
            written = await _client.ReplaceAsync(takeover, cancellationToken).ConfigureAwait(false);
        }

        if (!written.TryGetValue(out var document))
        {
            return written.Error!;
        }

        LogAcquired(_logger, name, _holder);
        return Result.Success<LeaderLease>(new KubernetesLeaderLease(_client, _clock, resource, _holder, timeToLive, document));
    }

    /// <summary>
    /// 取得租约。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired Kubernetes lease '{Lease}' as '{Holder}'.")]
    private static partial void LogAcquired(ILogger logger, string lease, string holder);
}
