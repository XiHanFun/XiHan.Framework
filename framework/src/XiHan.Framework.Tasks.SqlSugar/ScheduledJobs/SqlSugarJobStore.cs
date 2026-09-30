// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Mapping;
using XiHan.Framework.Tasks.SqlSugar.Options;

namespace XiHan.Framework.Tasks.SqlSugar.ScheduledJobs;

/// <summary>
/// 定时任务存储的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 任务实例与执行历史写入默认布局的主库。
/// 运行中的实例在开始时间加任务超时再加 <see cref="XiHanTasksSqlSugarOptions.RunningInstanceGracePeriod"/> 之后不再视为运行中。
/// </remarks>
public class SqlSugarJobStore : IJobStore
{
    private readonly TasksHostClientAccessor _clientAccessor;
    private readonly XiHanTasksSqlSugarOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientAccessor">宿主上下文客户端访问器</param>
    /// <param name="options">任务存储配置</param>
    public SqlSugarJobStore(
        TasksHostClientAccessor clientAccessor,
        IOptions<XiHanTasksSqlSugarOptions> options)
    {
        _clientAccessor = clientAccessor;
        _options = options.Value;
    }

    /// <summary>
    /// 保存任务实例，已存在则更新
    /// </summary>
    /// <remarks>
    /// 终止状态且完成时间为空时，把完成时间补为当前时间并回写到传入的实例上。
    /// </remarks>
    /// <param name="jobInstance">任务实例</param>
    public async Task SaveJobInstanceAsync(JobInstance jobInstance)
    {
        ArgumentNullException.ThrowIfNull(jobInstance);

        if (IsTerminal(jobInstance.Status))
        {
            jobInstance.CompletedAt ??= DateTimeOffset.UtcNow;
        }

        var entity = JobStoreMapper.ToEntity(jobInstance, _options.RunningInstanceGracePeriod);
        var instanceId = entity.BasicId;

        await _clientAccessor.ExecuteAsync(async client =>
        {
            var exists = await client.Queryable<SysJobInstance>()
                .AnyAsync(item => item.BasicId == instanceId);

            if (exists)
            {
                await client.Updateable(entity).ExecuteCommandAsync();
            }
            else
            {
                await client.Insertable(entity).ExecuteCommandAsync();
            }
        });
    }

    /// <summary>
    /// 更新任务实例状态，实例不存在时不做任何事
    /// </summary>
    /// <remarks>
    /// 终止状态且完成时间为空时，把完成时间写为当前时间；已有完成时间保持不变。
    /// </remarks>
    /// <param name="instanceId">实例唯一标识</param>
    /// <param name="status">状态</param>
    public async Task UpdateJobStatusAsync(string instanceId, JobStatus status)
    {
        ArgumentNullException.ThrowIfNull(instanceId);

        var statusValue = (int)status;

        if (IsTerminal(status))
        {
            var completedAt = DateTime.UtcNow;

            await _clientAccessor.ExecuteAsync(client => client.Updateable<SysJobInstance>()
                .SetColumns(item => new SysJobInstance
                {
                    Status = statusValue
                })
                .Where(item => item.BasicId == instanceId)
                .ExecuteCommandAsync());

            await _clientAccessor.ExecuteAsync(client => client.Updateable<SysJobInstance>()
                .SetColumns(item => new SysJobInstance
                {
                    CompletedAt = completedAt
                })
                .Where(item => item.BasicId == instanceId && item.CompletedAt == null)
                .ExecuteCommandAsync());

            return;
        }

        await _clientAccessor.ExecuteAsync(client => client.Updateable<SysJobInstance>()
            .SetColumns(item => new SysJobInstance
            {
                Status = statusValue
            })
            .Where(item => item.BasicId == instanceId)
            .ExecuteCommandAsync());
    }

    /// <summary>
    /// 保存任务执行历史，历史标识为空时生成新标识并回写到传入的历史上
    /// </summary>
    /// <param name="history">执行历史</param>
    public async Task SaveJobHistoryAsync(JobHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (string.IsNullOrWhiteSpace(history.HistoryId))
        {
            history.HistoryId = Guid.NewGuid().ToString("N");
        }

        var entity = JobStoreMapper.ToEntity(history);
        var historyId = entity.BasicId;

        await _clientAccessor.ExecuteAsync(async client =>
        {
            var exists = await client.Queryable<SysJobHistory>()
                .AnyAsync(item => item.BasicId == historyId);

            if (exists)
            {
                await client.Updateable(entity).ExecuteCommandAsync();
            }
            else
            {
                await client.Insertable(entity).ExecuteCommandAsync();
            }
        });
    }

    /// <summary>
    /// 获取任务实例
    /// </summary>
    /// <param name="instanceId">实例唯一标识</param>
    /// <returns>任务实例，不存在则为 null</returns>
    public async Task<JobInstance?> GetJobInstanceAsync(string instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);

        var entity = await _clientAccessor.ExecuteAsync(client => client.Queryable<SysJobInstance>()
            .Where(item => item.BasicId == instanceId)
            .FirstAsync());

        return entity is null ? null : JobStoreMapper.ToJobInstance(entity);
    }

    /// <summary>
    /// 获取任务执行历史，按开始时间倒序分页
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="pageIndex">页码，从 1 开始</param>
    /// <param name="pageSize">页大小</param>
    /// <returns>执行历史列表</returns>
    public async Task<IReadOnlyList<JobHistory>> GetJobHistoryAsync(string jobName, int pageIndex = 1, int pageSize = 20)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        if (pageIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "页码必须大于等于 1。");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "页大小必须大于等于 1。");
        }

        var entities = await _clientAccessor.ExecuteAsync(client => client.Queryable<SysJobHistory>()
            .Where(item => item.JobName == jobName)
            .OrderBy(item => item.StartedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize));

        return [.. entities.Select(JobStoreMapper.ToJobHistory)];
    }

    /// <summary>
    /// 获取运行中的任务实例，运行截止时刻已过的实例不计入
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <returns>运行中的任务实例列表</returns>
    public async Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var runningStatus = (int)JobStatus.Running;
        var now = DateTime.UtcNow;

        var entities = await _clientAccessor.ExecuteAsync(client => client.Queryable<SysJobInstance>()
            .Where(item => item.JobName == jobName
                && item.Status == runningStatus
                && item.RunningDeadline != null
                && item.RunningDeadline > now)
            .ToListAsync());

        return [.. entities.Select(JobStoreMapper.ToJobInstance)];
    }

    /// <summary>
    /// 清理过期的执行历史与已结束的任务实例
    /// </summary>
    /// <remarks>
    /// 删除开始时间早于保留期的执行历史，状态为成功、失败或已取消且完成时间早于保留期的任务实例，
    /// 以及状态为运行中且运行截止时刻早于保留期的遗留实例。
    /// </remarks>
    /// <param name="retentionDays">保留天数</param>
    public async Task CleanupHistoryAsync(int retentionDays)
    {
        if (retentionDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), retentionDays, "保留天数不能小于 0。");
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var succeeded = (int)JobStatus.Succeeded;
        var failed = (int)JobStatus.Failed;
        var canceled = (int)JobStatus.Canceled;
        var running = (int)JobStatus.Running;

        await _clientAccessor.ExecuteAsync(async client =>
        {
            await client.Deleteable<SysJobHistory>()
                .Where(item => item.StartedAt < cutoff)
                .ExecuteCommandAsync();

            await client.Deleteable<SysJobInstance>()
                .Where(item => (item.Status == succeeded || item.Status == failed || item.Status == canceled)
                    && item.CompletedAt != null
                    && item.CompletedAt < cutoff)
                .ExecuteCommandAsync();

            await client.Deleteable<SysJobInstance>()
                .Where(item => item.Status == running
                    && item.RunningDeadline != null
                    && item.RunningDeadline < cutoff)
                .ExecuteCommandAsync();
        });
    }

    /// <summary>
    /// 分批清理早于截止时间的执行历史与已结束的任务实例
    /// </summary>
    /// <remarks>
    /// 执行历史与任务实例各自先查出最多 <paramref name="batchSize"/> 条主键，再按主键删除。
    /// 实例的删除条件与 <see cref="CleanupHistoryAsync(int)"/> 一致：成功、失败或已取消且完成时间早于截止时间，
    /// 或运行中且运行截止时刻早于截止时间；等待中与运行截止时刻未到截止时间的运行中实例不删除。
    /// </remarks>
    /// <param name="cutoff">截止时间，早于该时间的记录被清理</param>
    /// <param name="batchSize">每类记录本批最多删除的条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本批删除的总条数</returns>
    public async Task<int> CleanupHistoryAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "每批删除条数必须大于 0。");
        }

        var cutoffTime = cutoff.UtcDateTime;
        var succeeded = (int)JobStatus.Succeeded;
        var failed = (int)JobStatus.Failed;
        var canceled = (int)JobStatus.Canceled;
        var running = (int)JobStatus.Running;

        cancellationToken.ThrowIfCancellationRequested();

        var deletedHistories = await _clientAccessor.ExecuteAsync(async client =>
        {
            var historyIds = await client.Queryable<SysJobHistory>()
                .Where(item => item.StartedAt < cutoffTime)
                .OrderBy(item => item.StartedAt)
                .Take(batchSize)
                .Select(item => item.BasicId)
                .ToListAsync(cancellationToken);

            if (historyIds.Count == 0)
            {
                return 0;
            }

            return await client.Deleteable<SysJobHistory>()
                .Where(item => historyIds.Contains(item.BasicId))
                .ExecuteCommandAsync(cancellationToken);
        });

        cancellationToken.ThrowIfCancellationRequested();

        var deletedInstances = await _clientAccessor.ExecuteAsync(async client =>
        {
            var instanceIds = await client.Queryable<SysJobInstance>()
                .Where(item => ((item.Status == succeeded || item.Status == failed || item.Status == canceled)
                        && item.CompletedAt != null
                        && item.CompletedAt < cutoffTime)
                    || (item.Status == running
                        && item.RunningDeadline != null
                        && item.RunningDeadline < cutoffTime))
                .Take(batchSize)
                .Select(item => item.BasicId)
                .ToListAsync(cancellationToken);

            if (instanceIds.Count == 0)
            {
                return 0;
            }

            return await client.Deleteable<SysJobInstance>()
                .Where(item => instanceIds.Contains(item.BasicId)
                    && (((item.Status == succeeded || item.Status == failed || item.Status == canceled)
                            && item.CompletedAt != null
                            && item.CompletedAt < cutoffTime)
                        || (item.Status == running
                            && item.RunningDeadline != null
                            && item.RunningDeadline < cutoffTime)))
                .ExecuteCommandAsync(cancellationToken);
        });

        return deletedHistories + deletedInstances;
    }

    /// <summary>
    /// 判断是否为终止状态
    /// </summary>
    private static bool IsTerminal(JobStatus status)
    {
        return status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Canceled;
    }
}
