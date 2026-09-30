// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Mapping;
using XiHan.Framework.Tasks.SqlSugar.Options;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.SqlSugar.BackgroundJobs;

/// <summary>
/// 后台作业存储的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 作业行写入默认布局的主库；存在事务型环境工作单元时，入队参与该工作单元的事务。
/// 支持逐作业租约与作业管理：续租、按令牌完成、按令牌回写、释放租约、重试与取消的每一步都是带主键（与令牌）条件的更新或删除，
/// 租约时长取 <see cref="XiHanTasksSqlSugarOptions.BackgroundJobLeaseTimeout"/>。放弃的作业保留在表中。
/// </remarks>
public class SqlSugarBackgroundJobStore : IBackgroundJobStore
{
    private readonly TasksHostClientAccessor _clientAccessor;
    private readonly IClock _clock;
    private readonly XiHanTasksSqlSugarOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientAccessor">宿主上下文客户端访问器</param>
    /// <param name="clock">时钟</param>
    /// <param name="options">任务存储配置</param>
    public SqlSugarBackgroundJobStore(
        TasksHostClientAccessor clientAccessor,
        IClock clock,
        IOptions<XiHanTasksSqlSugarOptions> options)
    {
        _clientAccessor = clientAccessor;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>
    /// 是否支持逐作业租约
    /// </summary>
    public bool SupportsJobLease => true;

    /// <summary>
    /// 是否支持作业管理
    /// </summary>
    public bool SupportsJobManagement => true;

    /// <summary>
    /// 按标识查找作业，已放弃的作业同样返回
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <returns>作业信息，不存在则为 null</returns>
    public async Task<BackgroundJobInfo?> FindAsync(Guid jobId)
    {
        var entity = await _clientAccessor.ExecuteAsync(client => client.Queryable<SysBackgroundJob>()
            .Where(item => item.BasicId == jobId)
            .FirstAsync());

        return entity is null ? null : ToJobInfo(entity, entity.ClaimTime);
    }

    /// <summary>
    /// 插入作业
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <returns>任务</returns>
    public async Task InsertAsync(BackgroundJobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);

        var entity = BackgroundJobMapper.ToEntity(jobInfo);

        await _clientAccessor.ExecuteAsync(client => client.Insertable(entity).ExecuteCommandAsync());
    }

    /// <summary>
    /// 领取一批待执行作业
    /// </summary>
    /// <remarks>
    /// 本方法在返回前会为作业盖上领取令牌与领取时刻，不是纯查询。
    /// 过滤条件为应用名相等、未放弃、下次执行时间不晚于当前时间、且未被领取或租约已过期；
    /// 按优先级降序、已尝试次数升序、下次执行时间升序排序。
    /// 租约时长由 <see cref="XiHanTasksSqlSugarOptions.BackgroundJobLeaseTimeout"/> 配置，
    /// 删除或更新作业会结束租约。应用名为空与空字符串视为同一个应用。
    /// 实际领取数量取 <paramref name="maxResultCount"/> 与 <see cref="XiHanTasksSqlSugarOptions.MaxClaimBatchSize"/> 的较小者。
    /// </remarks>
    /// <param name="applicationName">应用名</param>
    /// <param name="maxResultCount">最大返回数量</param>
    /// <returns>本次领取到的作业</returns>
    public async Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount)
    {
        if (maxResultCount <= 0)
        {
            return [];
        }

        var applicationKey = BackgroundJobMapper.ToApplicationKey(applicationName);

        var claimCount = Math.Min(maxResultCount, _options.MaxClaimBatchSize);

        var (claimed, claimTime) = await _clientAccessor.ExecuteAsync(client => ClaimAsync(client, applicationKey, claimCount));

        return [.. claimed.Select(entity => ToJobInfo(entity, claimTime))];
    }

    /// <summary>
    /// 在指定库上领取一批作业
    /// </summary>
    /// <remarks>
    /// 选中的候选被其他实例抢先领走时另选一批重试，最多三轮；返回空集合表示确实没有可领取的作业。
    /// </remarks>
    /// <param name="client">客户端</param>
    /// <param name="applicationKey">应用名存储键</param>
    /// <param name="maxResultCount">最多领取的条数</param>
    /// <returns>本次领取到的作业实体与领取时刻</returns>
    private async Task<(List<SysBackgroundJob> Claimed, DateTime ClaimTime)> ClaimAsync(
        ISqlSugarClient client,
        string applicationKey,
        int maxResultCount)
    {
        const int maxClaimAttempts = 3;

        for (var attempt = 0; attempt < maxClaimAttempts; attempt++)
        {
            var now = _clock.Now;
            var leaseExpiredBefore = now - _options.BackgroundJobLeaseTimeout;
            var claimToken = Guid.NewGuid().ToString("N");

            var candidateIds = await client.Queryable<SysBackgroundJob>()
                .Where(item => item.ApplicationName == applicationKey
                    && item.IsAbandoned == false
                    && item.NextTryTime <= now
                    && (item.ClaimTime == null || item.ClaimTime < leaseExpiredBefore))
                .OrderBy(item => item.Priority, OrderByType.Desc)
                .OrderBy(item => item.TryCount)
                .OrderBy(item => item.NextTryTime)
                .Take(maxResultCount)
                .Select(item => item.BasicId)
                .ToListAsync();

            if (candidateIds.Count == 0)
            {
                return ([], now);
            }

            var affected = await client.Updateable<SysBackgroundJob>()
                .SetColumns(item => new SysBackgroundJob
                {
                    ClaimToken = claimToken,
                    ClaimTime = now
                })
                .Where(item => candidateIds.Contains(item.BasicId)
                    && item.ApplicationName == applicationKey
                    && item.IsAbandoned == false
                    && item.NextTryTime <= now
                    && (item.ClaimTime == null || item.ClaimTime < leaseExpiredBefore))
                .ExecuteCommandAsync();

            // 候选全被其他实例抢走，另选一批重试
            if (affected == 0)
            {
                continue;
            }

            var claimed = await client.Queryable<SysBackgroundJob>()
                .Where(item => item.ClaimToken == claimToken)
                .OrderBy(item => item.Priority, OrderByType.Desc)
                .OrderBy(item => item.TryCount)
                .OrderBy(item => item.NextTryTime)
                .ToListAsync();

            return (claimed, now);
        }

        return ([], _clock.Now);
    }

    /// <summary>
    /// 删除作业
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <returns>任务</returns>
    public async Task DeleteAsync(Guid jobId)
    {
        await _clientAccessor.ExecuteAsync(client => client.Deleteable<SysBackgroundJob>()
            .Where(item => item.BasicId == jobId)
            .ExecuteCommandAsync());
    }

    /// <summary>
    /// 更新作业并释放领取租约，作业不存在时不插入
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <returns>任务</returns>
    public async Task UpdateAsync(BackgroundJobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);

        var entity = BackgroundJobMapper.ToEntity(jobInfo);

        await _clientAccessor.ExecuteAsync(client => client.Updateable(entity).ExecuteCommandAsync());
    }

    /// <summary>
    /// 续租：令牌匹配且领取时刻未早于租约过期界限时把领取时刻推进到当前时间
    /// </summary>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>续租后的租约（携带最新的取消请求标记）；令牌不匹配、租约已过期或作业不存在时返回 null</returns>
    public async Task<BackgroundJobLease?> TryRenewLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var jobId = lease.JobId;
        var token = lease.Token;
        var now = _clock.Now;
        var leaseValidSince = now - _options.BackgroundJobLeaseTimeout;

        return await _clientAccessor.ExecuteAsync(async client =>
        {
            var affected = await client.Updateable<SysBackgroundJob>()
                .SetColumns(item => new SysBackgroundJob
                {
                    ClaimTime = now
                })
                .Where(item => item.BasicId == jobId
                    && item.ClaimToken == token
                    && item.ClaimTime != null
                    && item.ClaimTime >= leaseValidSince)
                .ExecuteCommandAsync(cancellationToken);

            if (affected == 0)
            {
                return null;
            }

            var cancellationRequested = await client.Queryable<SysBackgroundJob>()
                .Where(item => item.BasicId == jobId
                    && item.ClaimToken == token
                    && item.IsCancellationRequested == true)
                .AnyAsync(cancellationToken);

            return new BackgroundJobLease(jobId, token, now + _options.BackgroundJobLeaseTimeout, cancellationRequested);
        });
    }

    /// <summary>
    /// 按令牌完成作业（删除）；令牌不匹配时不做任何变更
    /// </summary>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否命中</returns>
    public async Task<bool> TryCompleteAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var jobId = lease.JobId;
        var token = lease.Token;

        var affected = await _clientAccessor.ExecuteAsync(client => client.Deleteable<SysBackgroundJob>()
            .Where(item => item.BasicId == jobId && item.ClaimToken == token)
            .ExecuteCommandAsync(cancellationToken));

        return affected > 0;
    }

    /// <summary>
    /// 按令牌回写作业（失败退避 / 放弃 / 取消）并结束租约；令牌不匹配时不做任何变更
    /// </summary>
    /// <remarks>
    /// 取消请求取存储值与回写值的并集，并集为真时作业一律标记放弃；放弃的作业保留在表中。
    /// </remarks>
    /// <param name="jobInfo">作业信息</param>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否命中</returns>
    public async Task<bool> TryUpdateAsync(BackgroundJobInfo jobInfo, BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);
        ArgumentNullException.ThrowIfNull(lease);

        var jobId = lease.JobId;
        var token = lease.Token;
        var tryCount = jobInfo.TryCount;
        var nextTryTime = jobInfo.NextTryTime;
        var lastTryTime = jobInfo.LastTryTime;
        var cancellationRequested = jobInfo.IsCancellationRequested;
        var isAbandoned = jobInfo.IsAbandoned || cancellationRequested;
        bool? cancellationFlag = cancellationRequested ? true : null;

        return await _clientAccessor.ExecuteAsync(async client =>
        {
            var affected = await client.Updateable<SysBackgroundJob>()
                .SetColumns(item => new SysBackgroundJob
                {
                    TryCount = tryCount,
                    NextTryTime = nextTryTime,
                    LastTryTime = lastTryTime,
                    IsAbandoned = isAbandoned,
                    IsCancellationRequested = cancellationFlag,
                    ClaimToken = null,
                    ClaimTime = null
                })
                .Where(item => item.BasicId == jobId
                    && item.ClaimToken == token
                    && (item.IsCancellationRequested == null || item.IsCancellationRequested == false))
                .ExecuteCommandAsync(cancellationToken);

            if (affected > 0)
            {
                return true;
            }

            affected = await client.Updateable<SysBackgroundJob>()
                .SetColumns(item => new SysBackgroundJob
                {
                    TryCount = tryCount,
                    LastTryTime = lastTryTime,
                    IsAbandoned = true,
                    IsCancellationRequested = true,
                    ClaimToken = null,
                    ClaimTime = null
                })
                .Where(item => item.BasicId == jobId && item.ClaimToken == token)
                .ExecuteCommandAsync(cancellationToken);

            return affected > 0;
        });
    }

    /// <summary>
    /// 按令牌释放租约，作业保持待执行；令牌不匹配时不做任何变更
    /// </summary>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task ReleaseLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var jobId = lease.JobId;
        var token = lease.Token;

        await _clientAccessor.ExecuteAsync(client => client.Updateable<SysBackgroundJob>()
            .SetColumns(item => new SysBackgroundJob
            {
                ClaimToken = null,
                ClaimTime = null
            })
            .Where(item => item.BasicId == jobId && item.ClaimToken == token)
            .ExecuteCommandAsync(cancellationToken));
    }

    /// <summary>
    /// 把已放弃的作业重新排入待执行：清除放弃与取消标记、尝试次数归零、下次执行时间设为当前时间、结束租约
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Rescheduled / NoChange（未处于放弃状态）/ NotFound</returns>
    public async Task<BackgroundJobManagementStatus> RetryAbandonedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var now = _clock.Now;

        return await _clientAccessor.ExecuteAsync(async client =>
        {
            var affected = await client.Updateable<SysBackgroundJob>()
                .SetColumns(item => new SysBackgroundJob
                {
                    IsAbandoned = false,
                    IsCancellationRequested = null,
                    TryCount = 0,
                    NextTryTime = now,
                    ClaimToken = null,
                    ClaimTime = null
                })
                .Where(item => item.BasicId == jobId && item.IsAbandoned == true)
                .ExecuteCommandAsync(cancellationToken);

            if (affected > 0)
            {
                return BackgroundJobManagementStatus.Rescheduled;
            }

            return await ExistsAsync(client, jobId, cancellationToken)
                ? BackgroundJobManagementStatus.NoChange
                : BackgroundJobManagementStatus.NotFound;
        });
    }

    /// <summary>
    /// 请求取消作业：持有有效租约的作业登记取消请求；其余未放弃的作业直接标记放弃并结束租约
    /// </summary>
    /// <remarks>
    /// 两步条件更新都未命中、而作业仍未放弃且未登记取消请求时（两步之间作业被领取或释放），重新执行两步，最多三轮。
    /// </remarks>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>CancellationRequested / Cancelled / NoChange（已放弃或已请求）/ NotFound</returns>
    public async Task<BackgroundJobManagementStatus> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 3;

        return await _clientAccessor.ExecuteAsync(async client =>
        {
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var leaseValidSince = _clock.Now - _options.BackgroundJobLeaseTimeout;

                var requested = await client.Updateable<SysBackgroundJob>()
                    .SetColumns(item => new SysBackgroundJob
                    {
                        IsCancellationRequested = true
                    })
                    .Where(item => item.BasicId == jobId
                        && item.IsAbandoned == false
                        && item.ClaimToken != null
                        && item.ClaimTime != null
                        && item.ClaimTime >= leaseValidSince
                        && (item.IsCancellationRequested == null || item.IsCancellationRequested == false))
                    .ExecuteCommandAsync(cancellationToken);

                if (requested > 0)
                {
                    return BackgroundJobManagementStatus.CancellationRequested;
                }

                var cancelled = await client.Updateable<SysBackgroundJob>()
                    .SetColumns(item => new SysBackgroundJob
                    {
                        IsAbandoned = true,
                        IsCancellationRequested = true,
                        ClaimToken = null,
                        ClaimTime = null
                    })
                    .Where(item => item.BasicId == jobId
                        && item.IsAbandoned == false
                        && (item.ClaimToken == null || item.ClaimTime == null || item.ClaimTime < leaseValidSince))
                    .ExecuteCommandAsync(cancellationToken);

                if (cancelled > 0)
                {
                    return BackgroundJobManagementStatus.Cancelled;
                }

                var stored = await client.Queryable<SysBackgroundJob>()
                    .Where(item => item.BasicId == jobId)
                    .FirstAsync(cancellationToken);

                if (stored is null)
                {
                    return BackgroundJobManagementStatus.NotFound;
                }

                if (stored.IsAbandoned || stored.IsCancellationRequested == true)
                {
                    return BackgroundJobManagementStatus.NoChange;
                }
            }

            return await ExistsAsync(client, jobId, cancellationToken)
                ? BackgroundJobManagementStatus.NoChange
                : BackgroundJobManagementStatus.NotFound;
        });
    }

    /// <summary>
    /// 判断作业是否存在
    /// </summary>
    private static Task<bool> ExistsAsync(ISqlSugarClient client, Guid jobId, CancellationToken cancellationToken)
    {
        return client.Queryable<SysBackgroundJob>()
            .Where(item => item.BasicId == jobId)
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// 把实体转换为作业信息，持有令牌时按领取时刻加租约时长填写租约到期时间
    /// </summary>
    private BackgroundJobInfo ToJobInfo(SysBackgroundJob entity, DateTime? claimTime)
    {
        var info = BackgroundJobMapper.ToJobInfo(entity);
        if (info.ClaimToken is not null && claimTime.HasValue)
        {
            info.LeaseExpiresAt = claimTime.Value + _options.BackgroundJobLeaseTimeout;
        }

        return info;
    }
}
