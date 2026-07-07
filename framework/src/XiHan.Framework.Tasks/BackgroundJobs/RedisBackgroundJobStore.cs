#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RedisBackgroundJobStore
// Guid:68c32034-0509-4677-8d5c-afcbff8c19fb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using StackExchange.Redis;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 基于 Redis 的后台作业存储（持久化 + 跨实例）
/// </summary>
/// <remarks>
/// 数据结构：活跃作业索引用有序集合 <c>{Prefix}:index</c>（成员=作业 Id，score=下次执行时间毫秒）；
/// 作业体用字符串键 <c>{Prefix}:job:{id}</c>（JSON）。放弃的作业移出索引并给作业体设 TTL 便于事后排查。
/// 因框架 <see cref="BackgroundJobWorker"/> 已用分布式锁保证单活，本存储的领取无需再做原子租约。
/// </remarks>
public class RedisBackgroundJobStore : IBackgroundJobStore
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IClock _clock;
    private readonly IBackgroundJobSerializer _serializer;
    private readonly RedisBackgroundJobStoreOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RedisBackgroundJobStore(
        IConnectionMultiplexer connection,
        IClock clock,
        IBackgroundJobSerializer serializer,
        IOptions<RedisBackgroundJobStoreOptions> options)
    {
        _connection = connection;
        _clock = clock;
        _serializer = serializer;
        _options = options.Value;
    }

    private IDatabase Db => _connection.GetDatabase();

    private string IndexKey => $"{_options.KeyPrefix}:index";

    /// <summary>
    /// 按标识查找作业
    /// </summary>
    public async Task<BackgroundJobInfo?> FindAsync(Guid jobId)
    {
        var value = await Db.StringGetAsync(JobKey(jobId));
        return value.IsNullOrEmpty ? null : Deserialize(value);
    }

    /// <summary>
    /// 插入作业
    /// </summary>
    public async Task InsertAsync(BackgroundJobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);

        var db = Db;
        await db.StringSetAsync(JobKey(jobInfo.Id), Serialize(jobInfo));
        await db.SortedSetAddAsync(IndexKey, Member(jobInfo.Id), ToScore(jobInfo.NextTryTime));
    }

    /// <summary>
    /// 获取待执行作业（过滤 + 排序 + 限量，契约见接口）
    /// </summary>
    public async Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount)
    {
        var db = Db;
        var nowScore = ToScore(_clock.Now);
        var fetchCap = (long)maxResultCount * Math.Max(1, _options.FetchMultiplier);

        // 索引里 NextTryTime <= 现在 的到期候选（按 score 升序，限量）
        var ids = await db.SortedSetRangeByScoreAsync(
            IndexKey,
            double.NegativeInfinity,
            nowScore,
            Exclude.None,
            Order.Ascending,
            0,
            fetchCap);

        if (ids.Length == 0)
        {
            return [];
        }

        var keys = Array.ConvertAll(ids, id => (RedisKey)JobKey(id.ToString()));
        var values = await db.StringGetAsync(keys);

        var now = _clock.Now;
        var jobs = new List<BackgroundJobInfo>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i].IsNullOrEmpty)
            {
                // 索引存在但作业体已不在（被删/过期）：清理孤儿索引项
                await db.SortedSetRemoveAsync(IndexKey, ids[i]);
                continue;
            }

            var job = Deserialize(values[i]);
            if (job.IsAbandoned || job.NextTryTime > now)
            {
                continue;
            }

            if (!string.Equals(job.ApplicationName, applicationName, StringComparison.Ordinal))
            {
                continue;
            }

            jobs.Add(job);
        }

        return [.. jobs
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.TryCount)
            .ThenBy(x => x.NextTryTime)
            .Take(maxResultCount)];
    }

    /// <summary>
    /// 删除作业（执行成功后调用）
    /// </summary>
    public async Task DeleteAsync(Guid jobId)
    {
        var db = Db;
        await db.KeyDeleteAsync(JobKey(jobId));
        await db.SortedSetRemoveAsync(IndexKey, Member(jobId));
    }

    /// <summary>
    /// 更新作业（失败退避 / 放弃后回写）
    /// </summary>
    public async Task UpdateAsync(BackgroundJobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);

        var db = Db;
        if (jobInfo.IsAbandoned)
        {
            // 放弃：作业体保留（带 TTL 便于排查），移出活跃索引
            await db.StringSetAsync(JobKey(jobInfo.Id), Serialize(jobInfo), TimeSpan.FromDays(_options.AbandonedRetentionDays));
            await db.SortedSetRemoveAsync(IndexKey, Member(jobInfo.Id));
        }
        else
        {
            await db.StringSetAsync(JobKey(jobInfo.Id), Serialize(jobInfo));
            await db.SortedSetAddAsync(IndexKey, Member(jobInfo.Id), ToScore(jobInfo.NextTryTime));
        }
    }

    /// <summary>
    /// 作业体键
    /// </summary>
    private string JobKey(Guid jobId)
    {
        return $"{_options.KeyPrefix}:job:{jobId:N}";
    }

    /// <summary>
    /// 作业体键（按索引成员字符串）
    /// </summary>
    private string JobKey(string member)
    {
        return $"{_options.KeyPrefix}:job:{member}";
    }

    /// <summary>
    /// 索引成员（作业 Id 的 N 格式）
    /// </summary>
    private static RedisValue Member(Guid jobId)
    {
        return jobId.ToString("N");
    }

    /// <summary>
    /// 有序集合分数：以毫秒为单位（Ticks/万），保证与下次执行时间单调一致且落在 double 精确整数范围内
    /// </summary>
    private static double ToScore(DateTime dateTime)
    {
        return dateTime.Ticks / (double)TimeSpan.TicksPerMillisecond;
    }

    private string Serialize(BackgroundJobInfo jobInfo)
    {
        return _serializer.Serialize(jobInfo);
    }

    private BackgroundJobInfo Deserialize(RedisValue value)
    {
        return (BackgroundJobInfo)_serializer.Deserialize(value.ToString(), typeof(BackgroundJobInfo));
    }
}
