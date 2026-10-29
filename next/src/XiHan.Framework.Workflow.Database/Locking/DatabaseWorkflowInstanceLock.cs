// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Storage;

namespace XiHan.Framework.Workflow.Database.Locking;

/// <summary>
/// 租约式的分布式实例锁（单例）。
/// </summary>
/// <remarks>
/// 实例 ID 对应一行 <c>WorkflowLockRecord</c>，拿到后后台每三分之一租约续约一次，释放时删除自己的行；持有者消失时别的节点在租约到期后接管。等待超过 <see cref="WorkflowOptions.LockTimeout"/> → <see cref="WorkflowErrors.LockTimeout"/>；不可重入。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DatabaseWorkflowInstanceLock : IWorkflowInstanceLock
{
    /// <summary>
    /// 等待的起始间隔。
    /// </summary>
    private static readonly TimeSpan _initialDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// 等待间隔的上限。
    /// </summary>
    private static readonly TimeSpan _maxDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 租约行操作。
    /// </summary>
    private readonly LeaseTable _leases;

    /// <summary>
    /// 等待上限。
    /// </summary>
    private readonly TimeSpan _timeout;

    /// <summary>
    /// 租约时长。
    /// </summary>
    private readonly TimeSpan _lease;

    /// <summary>
    /// 本节点的持有者标识。
    /// </summary>
    private readonly string _holderId;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseWorkflowInstanceLock(IServiceProvider services, App app, WorkflowOptions options, DatabaseWorkflowOptions databaseOptions, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _leases = new LeaseTable(services, app, clock);
        _timeout = options.LockTimeout;
        _lease = databaseOptions.LockLease;
        _holderId = databaseOptions.HolderId;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DatabaseWorkflowInstanceLock>();
    }

    /// <inheritdoc />
    public async ValueTask<Result<IAsyncDisposable>> AcquireAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        // 每次获取一个独立的持有者标记：续约与释放只认自己这一次的行，同一节点的两个获取者之间同样互斥
        var holder = $"{_holderId}:{Guid.NewGuid():N}";
        var deadline = _clock.GetUtcNow() + _timeout;
        var delay = _initialDelay;
        Error? last = null;
        while (true)
        {
            var attempt = await _leases.TryAcquireAsync(instanceId, holder, _lease, cancellationToken).ConfigureAwait(false);
            if (attempt.TryGetValue(out var outcome))
            {
                if (outcome == LeaseAttempt.Acquired)
                {
                    return new Handle(this, instanceId, holder);
                }
            }
            else if (!IsRetryable(attempt.Error!))
            {
                return StoreInvocation.Unavailable(attempt.Error!);
            }
            else
            {
                last = attempt.Error;
            }

            var now = _clock.GetUtcNow();
            if (now >= deadline)
            {
                return new Error(WorkflowErrors.LockTimeout, WorkflowMessages.LockTimeout, MessageArguments.Of(("id", instanceId)), last);
            }

            // 撞上别的获取者立即重试；别人持有则 50ms 起每次翻倍、封顶 500ms 地等，最后一次不超过截止时刻
            if (outcome != LeaseAttempt.Contended)
            {
                var remaining = deadline - now;
                await Task.Delay(delay < remaining ? delay : remaining, _clock, cancellationToken).ConfigureAwait(false);
                delay = delay * 2 < _maxDelay ? delay * 2 : _maxDelay;
            }
        }
    }

    /// <summary>
    /// 持久化层的冲突与管道里的意外当作争用重试。
    /// </summary>
    /// <remarks>
    /// 跨节点同时插入同一行时唯一约束以异常出现，内核把它转成 Unexpected。直到等待超时。其余失败是存储不可达。
    /// </remarks>
    private static bool IsRetryable(Error error)
        => error.Code is PersistenceErrors.Concurrency or PersistenceErrors.Duplicate or PersistenceErrors.NotFound or KernelErrors.Unexpected;

    /// <summary>
    /// 续约循环。
    /// </summary>
    /// <remarks>
    /// 每三分之一租约续一次；续约失败只记日志，行被别人接管则停止。
    /// </remarks>
    private async Task RenewLoopAsync(string instanceId, string holder, CancellationToken stopping)
    {
        var interval = _lease / 3;
        try
        {
            while (true)
            {
                await Task.Delay(interval, _clock, stopping).ConfigureAwait(false);
                var renewed = await _leases.RenewAsync(instanceId, holder, _lease, stopping).ConfigureAwait(false);
                if (renewed.IsFailure)
                {
                    LogRenewFailed(_logger, instanceId, renewed.Error!.Code);
                    continue;
                }

                if (!renewed.ValueOrThrow())
                {
                    LogLeaseLost(_logger, instanceId);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // 释放时停止续约
        }
    }

    /// <summary>
    /// 释放。
    /// </summary>
    /// <remarks>
    /// 删除自己的行；失败只记日志，租约到期后行自然可被接管。
    /// </remarks>
    private async ValueTask ReleaseAsync(string instanceId, string holder)
    {
        var released = await _leases.ReleaseAsync(instanceId, holder, CancellationToken.None).ConfigureAwait(false);
        if (released.IsFailure)
        {
            LogReleaseFailed(_logger, instanceId, released.Error!.Code);
        }
    }

    /// <summary>
    /// 续约失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Renewing the lease of workflow instance '{InstanceId}' failed with '{ErrorCode}'.")]
    private static partial void LogRenewFailed(ILogger logger, string instanceId, string errorCode);

    /// <summary>
    /// 租约被接管。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The lease of workflow instance '{InstanceId}' was taken over by another holder; renewal stops.")]
    private static partial void LogLeaseLost(ILogger logger, string instanceId);

    /// <summary>
    /// 释放失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Releasing the lease of workflow instance '{InstanceId}' failed with '{ErrorCode}'; it expires on its own.")]
    private static partial void LogReleaseFailed(ILogger logger, string instanceId, string errorCode);

    /// <summary>
    /// 锁句柄。
    /// </summary>
    /// <remarks>
    /// 持有期间后台续约，释放时停止续约并删除自己的行；重复释放无害。
    /// </remarks>
    private sealed class Handle : IAsyncDisposable
    {
        /// <summary>
        /// 所属锁。
        /// </summary>
        private readonly DatabaseWorkflowInstanceLock _owner;

        /// <summary>
        /// 实例 ID。
        /// </summary>
        private readonly string _instanceId;

        /// <summary>
        /// 本次获取的持有者标记。
        /// </summary>
        private readonly string _holder;

        /// <summary>
        /// 停止续约的信号。
        /// </summary>
        private readonly CancellationTokenSource _stopping = new();

        /// <summary>
        /// 续约循环。
        /// </summary>
        private readonly Task _renewal;

        /// <summary>
        /// 是否已释放（0 未释放，1 已释放）。
        /// </summary>
        private int _released;

        /// <summary>
        /// 创建并开始续约。
        /// </summary>
        public Handle(DatabaseWorkflowInstanceLock owner, string instanceId, string holder)
        {
            _owner = owner;
            _instanceId = instanceId;
            _holder = holder;
            _renewal = owner.RenewLoopAsync(instanceId, holder, _stopping.Token);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            // 只有第一次释放做事：先停续约再删行，否则续约可能在删除之后把行写回来
            if (Interlocked.Exchange(ref _released, 1) == 1)
            {
                return;
            }

            await _stopping.CancelAsync().ConfigureAwait(false);
            await _renewal.ConfigureAwait(false);
            _stopping.Dispose();
            await _owner.ReleaseAsync(_instanceId, _holder).ConfigureAwait(false);
        }
    }
}
