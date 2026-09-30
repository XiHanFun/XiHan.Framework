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
    /// 按标识查找作业，已放弃的作业同样返回
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <returns>作业信息，不存在则为 null</returns>
    public async Task<BackgroundJobInfo?> FindAsync(Guid jobId)
    {
        var entity = await _clientAccessor.ExecuteAsync(client => client.Queryable<SysBackgroundJob>()
            .Where(item => item.BasicId == jobId)
            .FirstAsync());

        return entity is null ? null : BackgroundJobMapper.ToJobInfo(entity);
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

        var claimed = await _clientAccessor.ExecuteAsync(client => ClaimAsync(client, applicationKey, claimCount));

        return [.. claimed.Select(BackgroundJobMapper.ToJobInfo)];
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
    /// <returns>本次领取到的作业实体</returns>
    private async Task<List<SysBackgroundJob>> ClaimAsync(
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
                return [];
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

            return await client.Queryable<SysBackgroundJob>()
                .Where(item => item.ClaimToken == claimToken)
                .OrderBy(item => item.Priority, OrderByType.Desc)
                .OrderBy(item => item.TryCount)
                .OrderBy(item => item.NextTryTime)
                .ToListAsync();
        }

        return [];
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
}
