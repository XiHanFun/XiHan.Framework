// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Database.Locking;

namespace XiHan.Framework.Workflow.Database.Polling;

/// <summary>
/// 到期书签轮询的领导权（单例）。
/// </summary>
/// <remarks>
/// 用与实例锁同一张租约表、资源名 <see cref="Resource"/>。每个轮询周期调用一次 <see cref="TryHoldAsync"/>：没持有就尝试一次（不等待），持有就按需续约；拿不到的节点跳过本周期。领导权只为省资源：多个节点同时轮询也不会重复执行，到期书签的恢复由引擎的实例锁串行化。只在单个轮询循环里使用，不做并发保护。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class PollerLeadership
{
    /// <summary>
    /// 租约行的资源名。
    /// </summary>
    public const string Resource = "workflow.poller";

    /// <summary>
    /// 租约行操作。
    /// </summary>
    private readonly LeaseTable _leases;

    /// <summary>
    /// 本节点在这张表上的持有者标记：节点标识加随机后缀，同一节点重启后不与旧行混淆。
    /// </summary>
    private readonly string _holder;

    /// <summary>
    /// 租约时长。
    /// </summary>
    private readonly TimeSpan _lease;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 最近一次获取或续约成功的时刻。
    /// </summary>
    private DateTimeOffset _renewedAt;

    /// <summary>
    /// 创建。
    /// </summary>
    public PollerLeadership(IServiceProvider services, App app, DatabaseWorkflowOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _leases = new LeaseTable(services, app, clock);
        _holder = $"{options.HolderId}:{Guid.NewGuid():N}";
        _lease = options.LockLease;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<PollerLeadership>();
    }

    /// <summary>
    /// 当前是否持有领导权。
    /// </summary>
    public bool IsLeader { get; private set; }

    /// <summary>
    /// 没持有就尝试一次获取（不等待）。
    /// </summary>
    /// <remarks>
    /// 持有且距上次续约已过三分之一租约就续约；返回本周期是否持有。
    /// </remarks>
    public async ValueTask<bool> TryHoldAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (IsLeader)
        {
            // 续约失败但租约还没到期时仍视为持有：一次失败的数据库往返不该让轮询停下；到期了才放弃
            if (now - _renewedAt < _lease / 3)
            {
                return true;
            }

            var renewed = await _leases.RenewAsync(Resource, _holder, _lease, cancellationToken).ConfigureAwait(false);
            if (renewed.IsSuccess && renewed.ValueOrThrow())
            {
                _renewedAt = now;
                return true;
            }

            if (renewed.IsFailure)
            {
                LogRenewFailed(_logger, renewed.Error!.Code);
                if (now - _renewedAt < _lease)
                {
                    return true;
                }
            }

            IsLeader = false;
            LogLost(_logger, Resource);
            return false;
        }

        var attempt = await _leases.TryAcquireAsync(Resource, _holder, _lease, cancellationToken).ConfigureAwait(false);
        if (attempt.IsFailure)
        {
            LogAcquireFailed(_logger, attempt.Error!.Code);
            return false;
        }

        if (attempt.ValueOrThrow() != LeaseAttempt.Acquired)
        {
            return false;
        }

        IsLeader = true;
        _renewedAt = now;
        LogAcquired(_logger, Resource);
        return true;
    }

    /// <summary>
    /// 释放领导权（停止时调用）。
    /// </summary>
    /// <remarks>
    /// 没持有时什么也不做。
    /// </remarks>
    public async ValueTask ReleaseAsync(CancellationToken cancellationToken)
    {
        if (!IsLeader)
        {
            return;
        }

        IsLeader = false;
        var released = await _leases.ReleaseAsync(Resource, _holder, cancellationToken).ConfigureAwait(false);
        if (released.IsFailure)
        {
            LogReleaseFailed(_logger, released.Error!.Code);
        }
    }

    /// <summary>
    /// 拿到领导权。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired the workflow poller leadership '{Resource}'.")]
    private static partial void LogAcquired(ILogger logger, string resource);

    /// <summary>
    /// 失去领导权。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Lost the workflow poller leadership '{Resource}'.")]
    private static partial void LogLost(ILogger logger, string resource);

    /// <summary>
    /// 获取失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Acquiring the workflow poller leadership failed with '{ErrorCode}'.")]
    private static partial void LogAcquireFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 续约失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Renewing the workflow poller leadership failed with '{ErrorCode}'.")]
    private static partial void LogRenewFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 释放失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Releasing the workflow poller leadership failed with '{ErrorCode}'; it expires on its own.")]
    private static partial void LogReleaseFailed(ILogger logger, string errorCode);
}
