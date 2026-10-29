// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 经 <see cref="IDistributedLock"/> 租约分配的机器位。
/// </summary>
/// <remarks>
/// 启动时从 0 起逐个尝试锁 <c>persistence:snowflake:worker:&lt;n&gt;</c>，取得第一个空位；每三分之一存活时间续约一次（锁不续约，续约 = 释放再获取同一机器位），续约失败按丢失处理并重新分配。本地按获取时刻 + 存活时间判定有效：到期未续上即没有机器位，宁可拒绝生成也不冒重复的风险。同时是健康项 <see cref="HealthName"/> 的贡献者。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class LeasedWorkerIdSource : IWorkerIdSource, IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 健康项名：是否持有机器位租约。
    /// </summary>
    public const string HealthName = "persistence.snowflake.worker";

    /// <summary>
    /// 锁资源的前缀，后接机器位。
    /// </summary>
    public const string ResourcePrefix = "persistence:snowflake:worker:";

    /// <summary>
    /// 分布式锁。
    /// </summary>
    private readonly IDistributedLock _lock;

    /// <summary>
    /// 租约存活时间。
    /// </summary>
    private readonly TimeSpan _timeToLive;

    /// <summary>
    /// 时钟：本地有效期与续约计时器都按它计时。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 串行化获取、续约与释放：续约循环与停止可能同时碰租约。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 当前租约。
    /// </summary>
    /// <remarks>
    /// 整体替换，<see cref="Current"/> 读它不加锁。
    /// </remarks>
    private volatile Lease? _lease;

    /// <summary>
    /// 停止信号。
    /// </summary>
    /// <remarks>
    /// 非空表示续约循环在跑。
    /// </remarks>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 续约循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 是否已释放：容器按本类型与它实现的每个接口各释放一次，只有第一次生效。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="distributedLock">分布式锁；<c>caching.distributed</c> 的 Provider 注册。</param>
    /// <param name="options">选项；取 <see cref="SnowflakeOptions.LeaseTimeToLive"/>。</param>
    /// <param name="clock">时钟。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    public LeasedWorkerIdSource(IDistributedLock distributedLock, SnowflakeOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _lock = distributedLock;
        _timeToLive = options.LeaseTimeToLive;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<LeasedWorkerIdSource>();
    }

    /// <inheritdoc />
    public Maybe<int> Current
    {
        get
        {
            // 过期判定用本地时钟：获取时刻取在请求发出之前，所以本地到期不晚于存储侧到期，别的实例拿到同一机器位时本实例已经不再用它
            var lease = _lease;
            return lease is not null && _clock.GetUtcNow() < lease.ExpiresAt ? Maybe.Some(lease.WorkerId) : Maybe.None<int>();
        }
    }

    /// <summary>
    /// 当前租约的本地到期时刻。
    /// </summary>
    /// <remarks>
    /// 没有租约为 <see langword="null"/>。
    /// </remarks>
    public DateTimeOffset? ExpiresAt => _lease?.ExpiresAt;

    /// <summary>
    /// 续约间隔：存活时间的三分之一。
    /// </summary>
    public TimeSpan RenewInterval => _timeToLive / 3;

    /// <summary>
    /// 分配机器位并起续约循环。
    /// </summary>
    /// <remarks>
    /// 1024 个机器位都被占用时抛出携带 <see cref="PersistenceErrors.WorkerIdUnavailable"/> 的 <see cref="FrameworkException"/>。重复调用不再起第二个循环。
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await AcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new FrameworkException(PersistenceErrors.WorkerIdUnavailable, $"All {SnowflakeId.MaxWorkerId + 1} snowflake worker IDs are leased; no worker ID could be allocated.");
            }
        }
        finally
        {
            _gate.Release();
        }

        _stopping = new CancellationTokenSource();
        var stopping = _stopping.Token;
        // 计时器在这里同步创建：循环任务还没被调度时时钟已经推进（假时钟）也不会丢掉第一个周期
        var timer = new PeriodicTimer(RenewInterval, _clock);
        _loop = Task.Run(() => LoopAsync(timer, stopping), CancellationToken.None);
    }

    /// <summary>
    /// 停止续约循环并释放租约。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        if (_stopping is not null && _loop is not null)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            try
            {
                await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 循环按取消退出，或调用方等不及：两种情况都不再续约
            }

            _stopping.Dispose();
            _stopping = null;
            _loop = null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lease = _lease;
            _lease = null;
            if (lease is not null)
            {
                await lease.Handle.ReleaseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 续约一次。
    /// </summary>
    /// <remarks>
    /// 持有时释放再获取同一机器位，失败即视为丢失并重新分配；没有租约时直接重新分配。存储不可达时按丢失处理（存储侧租约可能已被删掉，继续用不安全）。
    /// </remarks>
    public async ValueTask RenewAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lease = _lease;
            if (lease is not null)
            {
                var acquiredAt = _clock.GetUtcNow();
                var handle = await TryRenewAsync(lease, cancellationToken).ConfigureAwait(false);
                if (handle is not null)
                {
                    _lease = new Lease(lease.WorkerId, handle, acquiredAt + _timeToLive);
                    return;
                }

                _lease = null;
                LogLost(_logger, lease.WorkerId);
            }

            if (!await AcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                LogUnavailable(_logger);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 锁存储不可达：当前租约已在 TryRenewAsync 里按丢失处理；下一个周期再试
            LogRenewFailed(_logger, exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check() =>
    [
        Current.HasValue
            ? new ContributorStatus(HealthName, LifecycleState.Ready)
            : new ContributorStatus(HealthName, LifecycleState.Degraded, Error.Unavailable(PersistenceErrors.WorkerIdUnavailable, PersistenceMessages.WorkerIdUnavailable)),
    ];

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        _gate.Dispose();
    }

    /// <summary>
    /// 从 0 起逐个尝试，取得第一个空位。
    /// </summary>
    /// <remarks>
    /// 全部被占返回 <see langword="false"/>。调用方持有 <see cref="_gate"/>。
    /// </remarks>
    private async Task<bool> AcquireAsync(CancellationToken cancellationToken)
    {
        for (var workerId = 0; workerId <= SnowflakeId.MaxWorkerId; workerId++)
        {
            // 获取时刻取在请求发出之前：本地到期不晚于存储侧到期
            var acquiredAt = _clock.GetUtcNow();
            var acquired = await _lock.TryAcquireAsync(Resource(workerId), _timeToLive, cancellationToken).ConfigureAwait(false);
            if (acquired.TryGetValue(out var handle))
            {
                _lease = new Lease(workerId, handle, acquiredAt + _timeToLive);
                LogAcquired(_logger, workerId, _lease.ExpiresAt);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 释放再获取同一机器位。
    /// </summary>
    /// <remarks>
    /// 任一步失败或抛出都先把租约置空再返回或传播——存储侧的租约可能已被删掉，继续用不安全。
    /// </remarks>
    private async Task<LockHandle?> TryRenewAsync(Lease lease, CancellationToken cancellationToken)
    {
        try
        {
            // 锁契约不续约：只能释放再获取。释放到重新获取之间空位可能被别的实例拿走，那就按丢失处理重新分配
            if (!await lease.Handle.ReleaseAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var renewed = await _lock.TryAcquireAsync(Resource(lease.WorkerId), _timeToLive, cancellationToken).ConfigureAwait(false);
            return renewed.TryGetValue(out var handle) ? handle : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _lease = null;
            LogLost(_logger, lease.WorkerId);
            throw;
        }
    }

    /// <summary>
    /// 按间隔续约，直到停止。
    /// </summary>
    /// <remarks>
    /// 计时器由本方法释放。
    /// </remarks>
    private async Task LoopAsync(PeriodicTimer timer, CancellationToken stopping)
    {
        using (timer)
        {
            try
            {
                while (await timer.WaitForNextTickAsync(stopping).ConfigureAwait(false))
                {
                    await RenewAsync(stopping).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 停止
            }
        }
    }

    /// <summary>
    /// 机器位对应的锁资源。
    /// </summary>
    private static CacheKey Resource(int workerId) => new(ResourcePrefix + workerId);

    /// <summary>
    /// 取得机器位。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Snowflake worker ID {WorkerId} leased until {ExpiresAt}.")]
    private static partial void LogAcquired(ILogger logger, int workerId, DateTimeOffset expiresAt);

    /// <summary>
    /// 机器位租约丢失。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Snowflake worker ID {WorkerId} lease was lost; a new worker ID will be allocated.")]
    private static partial void LogLost(ILogger logger, int workerId);

    /// <summary>
    /// 没有空闲机器位。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "No snowflake worker ID could be allocated: all worker IDs are leased; ID generation fails until one is available.")]
    private static partial void LogUnavailable(ILogger logger);

    /// <summary>
    /// 续约时锁存储不可达。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Snowflake worker ID lease renewal failed; the lease is treated as lost until the next attempt.")]
    private static partial void LogRenewFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 一份租约：机器位、锁句柄与本地到期时刻。
    /// </summary>
    /// <param name="WorkerId">机器位。</param>
    /// <param name="Handle">锁句柄。</param>
    /// <param name="ExpiresAt">本地到期时刻。</param>
    private sealed record Lease(int WorkerId, LockHandle Handle, DateTimeOffset ExpiresAt);
}
