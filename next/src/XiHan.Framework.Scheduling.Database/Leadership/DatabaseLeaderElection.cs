// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Database.Leadership;

/// <summary>
/// 以租约行实现的领导选举（单例）：资源名对应一行，行不存在就插入，过期就带并发戳接管，否则返回被占用；持有者是本节点的 ID（<see cref="SchedulerOptions.NodeId"/>），每次获取另带随机令牌，旧租约对象在释放或被接管后失效。
/// 存储不可达 → <see cref="SchedulingErrors.LeaseUnavailable"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DatabaseLeaderElection : ILeaderElection
{
    /// <summary>
    /// 租约行操作。
    /// </summary>
    private readonly LeaseRows _rows;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 本节点的持有者标识。
    /// </summary>
    private readonly string _holder;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseLeaderElection(IServiceProvider services, App app, SchedulerOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _rows = new LeaseRows(services, app, clock);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _holder = options.NodeId;
        _logger = loggerFactory.CreateLogger<DatabaseLeaderElection>();
    }

    /// <summary>
    /// 本节点的持有者标识。
    /// </summary>
    public string Holder => _holder;

    /// <inheritdoc />
    public async ValueTask<Result<LeaderLease>> TryAcquireAsync(JobName resource, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "The time to live must be positive.");
        }

        var token = Guid.NewGuid().ToString("N");
        var attempt = await _rows.TryAcquireAsync(resource.Value, _holder, token, timeToLive, cancellationToken).ConfigureAwait(false);
        if (!attempt.TryGetValue(out var expiresAt))
        {
            // 跨节点同时插入同一行时唯一约束以异常出现，内核把它转成 Unexpected：当作被占用，下个周期再试；其余是存储不可达
            return string.Equals(attempt.Error!.Code, KernelErrors.Unexpected, StringComparison.Ordinal) ? Held(resource) : Unavailable(resource, attempt.Error);
        }

        if (expiresAt is not { } expires)
        {
            return Held(resource);
        }

        LogAcquired(_logger, resource.Value, _holder);
        return Result.Success<LeaderLease>(new DatabaseLeaderLease(_rows, _clock, resource, _holder, token, timeToLive, expires, _logger));
    }

    /// <summary>
    /// 租约被占用。
    /// </summary>
    internal static Error Held(JobName resource)
        => Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", resource.Value)));

    /// <summary>
    /// 领导选举的存储不可达：以原错误码为原因、原错误为内部错误。
    /// </summary>
    internal static Error Unavailable(JobName resource, Error cause)
        => new(SchedulingErrors.LeaseUnavailable, SchedulingMessages.LeaseUnavailable, MessageArguments.Of(("resource", resource.Value), ("reason", cause.Code)), cause);

    /// <summary>
    /// 取得租约。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired the leader lease row for '{Resource}' as '{Holder}'.")]
    private static partial void LogAcquired(ILogger logger, string resource, string holder);
}
