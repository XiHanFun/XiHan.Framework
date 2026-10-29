// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;

namespace XiHan.Framework.Scheduling.Database.Leadership;

/// <summary>
/// 以租约行承载的租约。
/// </summary>
/// <remarks>
/// 到期时刻记在本地，持有与否按本地时钟判断；续约与释放回到表里比对本次获取的令牌，令牌不符即视为已被他人接管。
/// </remarks>
internal sealed partial class DatabaseLeaderLease : LeaderLease
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
    /// 本次获取的令牌。
    /// </summary>
    private readonly string _token;

    /// <summary>
    /// 存活时间。
    /// </summary>
    private readonly TimeSpan _timeToLive;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 最近一次获取或续约写入的到期时刻。
    /// </summary>
    private DateTimeOffset _expiresAt;

    /// <summary>
    /// 是否已释放或已失去。
    /// </summary>
    private bool _lost;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseLeaderLease(LeaseRows rows, TimeProvider clock, JobName resource, string holder, string token, TimeSpan timeToLive, DateTimeOffset expiresAt, ILogger logger)
        : base(resource, holder)
    {
        _rows = rows;
        _clock = clock;
        _token = token;
        _timeToLive = timeToLive;
        _expiresAt = expiresAt;
        _logger = logger;
    }

    /// <inheritdoc />
    public override DateTimeOffset ExpiresAt => _expiresAt;

    /// <inheritdoc />
    public override bool IsHeld => !_lost && _clock.GetUtcNow() < _expiresAt;

    /// <inheritdoc />
    public override async ValueTask<Result> RenewAsync(CancellationToken cancellationToken)
    {
        if (!IsHeld)
        {
            return DatabaseLeaderElection.Held(Resource);
        }

        var renewed = await _rows.RenewAsync(Resource.Value, _token, _timeToLive, cancellationToken).ConfigureAwait(false);
        if (!renewed.TryGetValue(out var expiresAt))
        {
            // 存储不可达不等于失去租约：到期前仍按持有对待，下次续约再试
            return DatabaseLeaderElection.Unavailable(Resource, renewed.Error!);
        }

        if (expiresAt is not { } extended)
        {
            _lost = true;
            return DatabaseLeaderElection.Held(Resource);
        }

        _expiresAt = extended;
        return Result.Success();
    }

    /// <inheritdoc />
    public override async ValueTask ReleaseAsync()
    {
        if (_lost)
        {
            return;
        }

        // 先在本地标记再删行：删行失败（不可达、已被接管）不影响释放语义，行到期后自然可被接管
        _lost = true;
        var released = await _rows.ReleaseAsync(Resource.Value, _token, CancellationToken.None).ConfigureAwait(false);
        if (released.IsFailure)
        {
            LogReleaseFailed(_logger, Resource.Value, released.Error!.Code);
        }
    }

    /// <summary>
    /// 释放失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Releasing the leader lease for '{Resource}' failed with '{ErrorCode}'; it expires on its own.")]
    private static partial void LogReleaseFailed(ILogger logger, string resource, string errorCode);
}
