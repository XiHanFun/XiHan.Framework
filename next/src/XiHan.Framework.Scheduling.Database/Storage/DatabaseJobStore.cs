// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using XiHan.Framework.Scheduling.Database.Entities;

namespace XiHan.Framework.Scheduling.Database.Storage;

/// <summary>
/// 数据库作业存储（单例，跨实例共享）。
/// </summary>
/// <remarks>
/// 作业、触发器、运行历史与节点各一张表，每个操作在根提供者的新作用域里经完整管道执行、返回即提交。抢占与独占占用靠并发戳做比较并交换：多个节点同时抢占同一条触发器只有先改成功的拿到。管道失败 → <see cref="SchedulingErrors.StoreUnavailable"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseJobStore : IJobStore
{
    /// <summary>
    /// 比较并交换撞上别的节点时的重试次数上限。
    /// </summary>
    private const int MaxAttempts = 3;

    /// <summary>
    /// 运行中状态的整数值。
    /// </summary>
    private const int RunningRun = (int)JobRunState.Running;

    /// <summary>
    /// 失联状态的整数值。
    /// </summary>
    private const int LostRun = (int)JobRunState.Lost;

    /// <summary>
    /// 等待状态的整数值。
    /// </summary>
    private const int WaitingTrigger = (int)TriggerState.Waiting;

    /// <summary>
    /// 已抢占状态的整数值。
    /// </summary>
    private const int AcquiredTrigger = (int)TriggerState.Acquired;

    /// <summary>
    /// 阻塞状态的整数值。
    /// </summary>
    private const int BlockedTrigger = (int)TriggerState.Blocked;

    /// <summary>
    /// 管道执行。
    /// </summary>
    private readonly StoreInvocation _invocation;

    /// <summary>
    /// 运行记录的写入序号。
    /// </summary>
    private readonly WriteSequence _sequence;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseJobStore(IServiceProvider services, App app, TimeProvider clock)
    {
        _invocation = new StoreInvocation(services, app);
        _sequence = new WriteSequence(clock);
    }

    /// <inheritdoc />
    public bool IsShared => true;

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<JobRecord>>> FindJobAsync(JobName name, CancellationToken cancellationToken)
        => StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var found = await StoreInvocation.Repository<JobEntity>(ctx).FindAsync(name.Value, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success(found.TryGetValue(out var row) ? Maybe.Some(RecordMapping.ToRecord(row)) : Maybe.None<JobRecord>());
        }, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<JobRecord>>> ListJobsAsync(CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<JobEntity>().OrderBy(static job => job.Id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<JobRecord>>(async ctx =>
        {
            var rows = await RecordQueries.ListAllAsync(StoreInvocation.Repository<JobEntity>(ctx), query, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<JobRecord>>(rows.Select(static row => RecordMapping.ToRecord(row)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveJobsAsync(IReadOnlyList<JobRecord> jobs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        var rows = jobs.Select(static job => RecordMapping.ToEntity(job)).ToList();
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(
            ctx => RecordQueries.UpsertAsync(StoreInvocation.Repository<JobEntity>(ctx), rows, RecordMapping.Copy, ctx.CancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteJobsAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        var ids = names.Select(static name => name.Value).ToList();
        if (ids.Count == 0)
        {
            return Result.Success();
        }

        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var triggers = StoreInvocation.Repository<TriggerEntity>(ctx);
            // 同一事务内先删触发器再删作业：作业删了触发器不会残留
            foreach (var batch in RecordQueries.Batches(ids))
            {
                var query = EntityQuery.All<TriggerEntity>()
                    .Where(new Specification<TriggerEntity>(trigger => batch.Contains(trigger.Job)))
                    .OrderBy(static trigger => trigger.Id);
                var owned = await RecordQueries.ListAllAsync(triggers, query, ctx.CancellationToken).ConfigureAwait(false);
                var removed = await RecordQueries.RemoveManyAsync(triggers, owned, ctx.CancellationToken).ConfigureAwait(false);
                if (removed.IsFailure)
                {
                    return removed.ToResult();
                }
            }

            var jobs = StoreInvocation.Repository<JobEntity>(ctx);
            var rows = await RecordQueries.FindManyAsync(jobs, ids, ctx.CancellationToken).ConfigureAwait(false);
            return (await RecordQueries.RemoveManyAsync(jobs, rows, ctx.CancellationToken).ConfigureAwait(false)).ToResult();
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<TriggerRecord>>> TriggersAsync(JobName? job, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<TriggerEntity>();
        if (job is { } name)
        {
            var value = name.Value;
            query = query.Where(new Specification<TriggerEntity>(trigger => trigger.Job == value));
        }

        // 作业名、触发器名升序；键文本的字典序与之不同（'.' 排在 '/' 前），所以不按 Id 排
        query = query.OrderBy(static trigger => trigger.Job).OrderBy(static trigger => trigger.Name);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<TriggerRecord>>(async ctx =>
        {
            var rows = await RecordQueries.ListAllAsync(StoreInvocation.Repository<TriggerEntity>(ctx), query, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<TriggerRecord>>(rows.Select(static row => RecordMapping.ToRecord(row)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveTriggersAsync(IReadOnlyList<TriggerRecord> triggers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        var rows = triggers.Select(static trigger => RecordMapping.ToEntity(trigger)).ToList();
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(
            ctx => RecordQueries.UpsertAsync(StoreInvocation.Repository<TriggerEntity>(ctx), rows, RecordMapping.Copy, ctx.CancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteTriggersAsync(IReadOnlyList<TriggerKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var ids = keys.Select(static key => key.ToString()).ToList();
        if (ids.Count == 0)
        {
            return Result.Success();
        }

        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var repository = StoreInvocation.Repository<TriggerEntity>(ctx);
            var rows = await RecordQueries.FindManyAsync(repository, ids, ctx.CancellationToken).ConfigureAwait(false);
            return (await RecordQueries.RemoveManyAsync(repository, rows, ctx.CancellationToken).ConfigureAwait(false)).ToResult();
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<TriggerRecord>>> AcquireDueAsync(DateTimeOffset dueBefore, int limit, string nodeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (limit <= 0)
        {
            return Result.Success<IReadOnlyList<TriggerRecord>>([]);
        }

        // 候选取名额的三倍：别的节点同时抢占时被抢走的那些跳过后仍有机会拿满
        var take = limit >= EntityQuery.DefaultMaxTake / 3 ? EntityQuery.DefaultMaxTake : limit * 3;
        var query = EntityQuery.All<TriggerEntity>()
            .Where(new Specification<TriggerEntity>(trigger => trigger.State == WaitingTrigger && trigger.Local == false && trigger.NextFireAt <= dueBefore))
            .OrderBy(static trigger => trigger.NextFireAt)
            .OrderBy(static trigger => trigger.Priority, descending: true)
            .OrderBy(static trigger => trigger.Id)
            .Page(0, take);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<TriggerRecord>>(async ctx =>
        {
            var repository = StoreInvocation.Repository<TriggerEntity>(ctx);
            var candidates = await repository.ListAsync(query, ctx.CancellationToken).ConfigureAwait(false);
            var acquired = new List<TriggerRecord>(Math.Min(limit, candidates.Count));
            var offset = 0;
            while (acquired.Count < limit && offset < candidates.Count)
            {
                // 一批只取还缺的名额：带并发戳的批量更新就是逐条比较并交换——读到候选之后被别的节点先改了（抢走、暂停、删除）的得到冲突，跳过它们用下一批补齐，不回滚已拿到的
                var chunk = candidates.Skip(offset).Take(limit - acquired.Count).ToList();
                offset += chunk.Count;
                foreach (var candidate in chunk)
                {
                    candidate.State = AcquiredTrigger;
                    candidate.AcquiredBy = nodeId;
                    candidate.AcquiredAt = now;
                }

                var results = await repository.UpdateRangeAsync(chunk, ctx.CancellationToken).ConfigureAwait(false);
                for (var index = 0; index < results.Count; index++)
                {
                    if (results[index].IsSuccess)
                    {
                        acquired.Add(RecordMapping.ToRecord(chunk[index]));
                    }
                    else if (!RecordQueries.IsRace(results[index].Error!))
                    {
                        return results[index].Error!;
                    }
                }
            }

            return Result.Success<IReadOnlyList<TriggerRecord>>(acquired);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<bool>> TryBeginRunAsync(JobRunRecord run, bool exclusive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var row = RecordMapping.ToEntity(run, _sequence.Next());
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<bool>(async ctx =>
        {
            var jobs = StoreInvocation.Repository<JobEntity>(ctx);
            var runs = StoreInvocation.Repository<JobRunEntity>(ctx);
            if (exclusive)
            {
                var occupied = await OccupyAsync(jobs, runs, run, ctx.CancellationToken).ConfigureAwait(false);
                if (!occupied.TryGetValue(out var started) || !started)
                {
                    return occupied;
                }
            }
            else if (!(await jobs.FindAsync(run.Job.Value, ctx.CancellationToken).ConfigureAwait(false)).HasValue)
            {
                return JobNotFound(run.Job);
            }

            var written = await RecordQueries.UpsertAsync(runs, [row], RecordMapping.Copy, ctx.CancellationToken).ConfigureAwait(false);
            return written.IsSuccess ? Result.Success(true) : written.Error!;
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> CompleteRunAsync(JobRunRecord run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var row = RecordMapping.ToEntity(run, _sequence.Next());
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var written = await RecordQueries.UpsertAsync(StoreInvocation.Repository<JobRunEntity>(ctx), [row], RecordMapping.Copy, ctx.CancellationToken).ConfigureAwait(false);
            if (written.IsFailure)
            {
                return written;
            }

            var released = await ReleaseJobAsync(StoreInvocation.Repository<JobEntity>(ctx), run.Job.Value, run.Id, ctx.CancellationToken).ConfigureAwait(false);
            return released.IsFailure ? released : await UnblockAsync(StoreInvocation.Repository<TriggerEntity>(ctx), run.Job.Value, ctx.CancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<bool>> BlockTriggerAsync(TriggerRecord trigger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var row = RecordMapping.ToEntity(trigger);
        row.AcquiredBy = null;
        row.AcquiredAt = null;
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<bool>(async ctx =>
        {
            var held = await HoldIfOccupiedAsync(StoreInvocation.Repository<JobEntity>(ctx), StoreInvocation.Repository<JobRunEntity>(ctx), trigger.Job, ctx.CancellationToken).ConfigureAwait(false);
            if (!held.TryGetValue(out var blocked))
            {
                return held;
            }

            row.State = blocked ? BlockedTrigger : WaitingTrigger;
            var written = await RecordQueries.UpsertAsync(StoreInvocation.Repository<TriggerEntity>(ctx), [row], RecordMapping.Copy, ctx.CancellationToken).ConfigureAwait(false);
            return written.IsSuccess ? Result.Success(blocked) : written.Error!;
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunsAsync(JobRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 计划时刻降序，同刻按写入序号降序，再按 ID 补成全序
        var filtered = Filter(query)
            .OrderBy(static run => run.ScheduledAt, descending: true)
            .OrderBy(static run => run.Sequence, descending: true)
            .OrderBy(static run => run.Id, descending: true)
            .Page(query.Skip, query.Limit);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<JobRunRecord>>(async ctx =>
        {
            var rows = await StoreInvocation.Repository<JobRunEntity>(ctx).ListAsync(filtered, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<JobRunRecord>>(rows.Select(static row => RecordMapping.ToRecord(row)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<long>> CountRunsAsync(JobRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filtered = Filter(query);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<long>(async ctx =>
            Result.Success(await StoreInvocation.Repository<JobRunEntity>(ctx).CountAsync(filtered, ctx.CancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunningAsync(JobName? job, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<JobRunEntity>().Where(new Specification<JobRunEntity>(run => run.State == RunningRun));
        if (job is { } name)
        {
            var value = name.Value;
            query = query.Where(new Specification<JobRunEntity>(run => run.Job == value));
        }

        query = query.OrderBy(static run => run.StartedAt).OrderBy(static run => run.Sequence).OrderBy(static run => run.Id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<JobRunRecord>>(async ctx =>
        {
            var rows = await RecordQueries.ListAllAsync(StoreInvocation.Repository<JobRunEntity>(ctx), query, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<JobRunRecord>>(rows.Select(static row => RecordMapping.ToRecord(row)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<JobRunRecord>>> LastRunsAsync(IReadOnlyList<JobName> jobs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        var names = jobs.Select(static job => job.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (names.Count == 0)
        {
            return Result.Success<IReadOnlyList<JobRunRecord>>([]);
        }

        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<JobRunRecord>>(async ctx =>
        {
            var repository = StoreInvocation.Repository<JobRunEntity>(ctx);
            var last = new List<JobRunRecord>(names.Count);
            foreach (var name in names)
            {
                // 每个作业一条：已结束的记录按结束时刻取最近的；已结束的行总带结束时刻，不另写空值条件
                var query = EntityQuery.All<JobRunEntity>()
                    .Where(new Specification<JobRunEntity>(run => run.Job == name && run.State != RunningRun))
                    .OrderBy(static run => run.CompletedAt, descending: true)
                    .OrderBy(static run => run.Sequence, descending: true)
                    .OrderBy(static run => run.Id, descending: true)
                    .Page(0, 1);
                var rows = await repository.ListAsync(query, ctx.CancellationToken).ConfigureAwait(false);
                if (rows.Count > 0 && rows[0].CompletedAt is not null)
                {
                    last.Add(RecordMapping.ToRecord(rows[0]));
                }
            }

            return Result.Success<IReadOnlyList<JobRunRecord>>(last);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> RequestCancelAsync(IReadOnlyList<string> runIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runIds);
        if (runIds.Count == 0)
        {
            return [];
        }

        var outcome = await _invocation.RunAsync<IReadOnlyList<Result>>(async ctx =>
        {
            var repository = StoreInvocation.Repository<JobRunEntity>(ctx);
            var results = new List<Result>(runIds.Count);
            foreach (var id in runIds)
            {
                results.Add(await RequestCancelOneAsync(repository, id, ctx.CancellationToken).ConfigureAwait(false));
            }

            return Result.Success<IReadOnlyList<Result>>(results);
        }, cancellationToken).ConfigureAwait(false);
        if (outcome.TryGetValue(out var results))
        {
            return results;
        }

        // 管道失败：每一项都报存储不可达
        var unavailable = StoreInvocation.Unavailable(outcome.Error!);
        return runIds.Select(_ => Result.Failure(unavailable)).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<string>>> CancelRequestedRunsAsync(string nodeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var query = EntityQuery.All<JobRunEntity>()
            .Where(new Specification<JobRunEntity>(run => run.State == RunningRun && run.CancelRequested == true && run.NodeId == nodeId))
            .OrderBy(static run => run.Sequence)
            .OrderBy(static run => run.Id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<string>>(async ctx =>
        {
            var rows = await RecordQueries.ListAllAsync(StoreInvocation.Repository<JobRunEntity>(ctx), query, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<string>>(rows.Select(static row => row.Id).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<int>> PurgeRunsAsync(DateTimeOffset before, CancellationToken cancellationToken)
    {
        // 未结束的行 CompletedAt 为空，NULL < 值 本就不成立，不另写空值条件
        var query = EntityQuery.All<JobRunEntity>()
            .Where(new Specification<JobRunEntity>(run => run.CompletedAt < before))
            .OrderBy(static run => run.Id)
            .Page(0, EntityQuery.DefaultMaxTake);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<int>(async ctx =>
        {
            var repository = StoreInvocation.Repository<JobRunEntity>(ctx);
            var purged = 0;
            while (true)
            {
                // 每次取第一页删掉再取：删过的行不再出现，不需要推进偏移
                var page = await repository.ListAsync(query, ctx.CancellationToken).ConfigureAwait(false);
                if (page.Count == 0)
                {
                    return purged;
                }

                var removed = await RecordQueries.RemoveManyAsync(repository, page, ctx.CancellationToken).ConfigureAwait(false);
                if (!removed.TryGetValue(out var count))
                {
                    return removed;
                }

                purged += count;
                if (page.Count < EntityQuery.DefaultMaxTake)
                {
                    return purged;
                }
            }
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> HeartbeatAsync(SchedulerNodeRecord node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        var row = RecordMapping.ToEntity(node);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(
            ctx => RecordQueries.UpsertAsync(StoreInvocation.Repository<SchedulerNodeEntity>(ctx), [row], RecordMapping.Copy, ctx.CancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<SchedulerNodeRecord>>> NodesAsync(CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<SchedulerNodeEntity>().OrderBy(static node => node.Id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<SchedulerNodeRecord>>(async ctx =>
        {
            var rows = await RecordQueries.ListAllAsync(StoreInvocation.Repository<SchedulerNodeEntity>(ctx), query, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<SchedulerNodeRecord>>(rows.Select(static row => RecordMapping.ToRecord(row)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<JobRunRecord>>> RecoverNodesAsync(IReadOnlyList<string> nodeIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        var dead = nodeIds.Distinct(StringComparer.Ordinal).ToList();
        if (dead.Count == 0)
        {
            return Result.Success<IReadOnlyList<JobRunRecord>>([]);
        }

        return StoreInvocation.OrUnavailable(await _invocation.RunAsync<IReadOnlyList<JobRunRecord>>(async ctx =>
        {
            var jobs = StoreInvocation.Repository<JobEntity>(ctx);
            var triggers = StoreInvocation.Repository<TriggerEntity>(ctx);
            // 先把失联节点的运行记为 Lost 并释放占用，再把触发器改回等待：阻塞的触发器要看释放后的占用才知道能否解除
            var lost = await MarkLostAsync(StoreInvocation.Repository<JobRunEntity>(ctx), dead, now, ctx.CancellationToken).ConfigureAwait(false);
            if (!lost.TryGetValue(out var lostRows))
            {
                return lost.Error!;
            }

            foreach (var run in lostRows)
            {
                var released = await ReleaseJobAsync(jobs, run.Job, run.Id, ctx.CancellationToken).ConfigureAwait(false);
                if (released.IsFailure)
                {
                    return released.Error!;
                }
            }

            var reset = await ResetAcquiredAsync(triggers, dead, now, ctx.CancellationToken).ConfigureAwait(false);
            if (reset.IsFailure)
            {
                return reset.Error!;
            }

            var unblocked = await UnblockOrphansAsync(jobs, triggers, ctx.CancellationToken).ConfigureAwait(false);
            if (unblocked.IsFailure)
            {
                return unblocked.Error!;
            }

            var nodes = StoreInvocation.Repository<SchedulerNodeEntity>(ctx);
            var removed = await RecordQueries.RemoveManyAsync(nodes, await RecordQueries.FindManyAsync(nodes, dead, ctx.CancellationToken).ConfigureAwait(false), ctx.CancellationToken).ConfigureAwait(false);
            if (removed.IsFailure)
            {
                return removed.Error!;
            }

            return Result.Success<IReadOnlyList<JobRunRecord>>(lostRows.OrderBy(static run => run.Id, StringComparer.Ordinal).Select(static row => RecordMapping.ToRecord(row)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 独占占用。
    /// </summary>
    /// <remarks>
    /// 作业不存在 → 作业未登记；作业上已有未结束的运行 → <see langword="false"/>；否则带并发戳把占用换成本次运行，撞上别的节点就重读再判，最多 <see cref="MaxAttempts"/> 次。
    /// </remarks>
    private static async ValueTask<Result<bool>> OccupyAsync(IRepository<JobEntity, string> jobs, IRepository<JobRunEntity, string> runs, JobRunRecord run, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var outcome = await TryOccupyOnceAsync(jobs, runs, run, cancellationToken).ConfigureAwait(false);
            // 别的节点刚改了作业行（开始或结束了一次运行）：重读后再判一次
            if (outcome.IsSuccess || !RecordQueries.IsRace(outcome.Error!) || attempt + 1 >= MaxAttempts)
            {
                return outcome;
            }
        }
    }

    /// <summary>
    /// 一次占用尝试。
    /// </summary>
    /// <remarks>
    /// 读作业行，占用指向的运行还在进行才算被占（结束了但没清掉的占用视为空闲），否则带并发戳写入本次运行。
    /// </remarks>
    private static async ValueTask<Result<bool>> TryOccupyOnceAsync(IRepository<JobEntity, string> jobs, IRepository<JobRunEntity, string> runs, JobRunRecord run, CancellationToken cancellationToken)
    {
        var found = await jobs.FindAsync(run.Job.Value, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var job))
        {
            return JobNotFound(run.Job);
        }

        if (job.ActiveRunId is { } active && await IsRunningAsync(runs, active, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        job.ActiveRunId = run.Id;
        var updated = await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        return updated.IsSuccess ? Result.Success(true) : updated.Error!;
    }

    /// <summary>
    /// 运行记录是否存在且在运行中。
    /// </summary>
    private static async ValueTask<bool> IsRunningAsync(IRepository<JobRunEntity, string> runs, string runId, CancellationToken cancellationToken)
    {
        var found = await runs.FindAsync(runId, cancellationToken).ConfigureAwait(false);
        return found.TryGetValue(out var row) && row.State == RunningRun;
    }

    /// <summary>
    /// 作业仍被占用时带并发戳改写一次作业行。
    /// </summary>
    /// <remarks>
    /// 运行结束（<see cref="CompleteRunAsync"/>）同样带并发戳改写这一行之后才解除阻塞：两边谁后提交谁看到对方，阻塞不会写在解除之后。撞上别的节点就重读再判，最多 <see cref="MaxAttempts"/> 次。作业不存在 → 作业未登记。
    /// </remarks>
    private static async ValueTask<Result<bool>> HoldIfOccupiedAsync(IRepository<JobEntity, string> jobs, IRepository<JobRunEntity, string> runs, JobName job, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var outcome = await TryHoldOnceAsync(jobs, runs, job, cancellationToken).ConfigureAwait(false);
            if (outcome.IsSuccess || !RecordQueries.IsRace(outcome.Error!) || attempt + 1 >= MaxAttempts)
            {
                return outcome;
            }
        }
    }

    /// <summary>
    /// 一次确认占用的尝试。
    /// </summary>
    /// <remarks>
    /// 占用指向的运行还在进行才算被占（与独占开始同一判定），被占时原样写回作业行换一个并发戳。
    /// </remarks>
    private static async ValueTask<Result<bool>> TryHoldOnceAsync(IRepository<JobEntity, string> jobs, IRepository<JobRunEntity, string> runs, JobName job, CancellationToken cancellationToken)
    {
        var found = await jobs.FindAsync(job.Value, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var row))
        {
            return JobNotFound(job);
        }

        if (row.ActiveRunId is not { } active || !await IsRunningAsync(runs, active, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var touched = await jobs.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
        return touched.IsSuccess ? Result.Success(true) : touched.Error!;
    }

    /// <summary>
    /// 作业的占用指向该运行时清掉。
    /// </summary>
    /// <remarks>
    /// 撞上别的节点就重读再试，最多 <see cref="MaxAttempts"/> 次。作业不存在或占用已不是它都视为已释放。
    /// </remarks>
    private static async ValueTask<Result> ReleaseJobAsync(IRepository<JobEntity, string> jobs, string job, string runId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var outcome = await TryReleaseOnceAsync(jobs, job, runId, cancellationToken).ConfigureAwait(false);
            if (outcome.IsSuccess || !RecordQueries.IsRace(outcome.Error!) || attempt + 1 >= MaxAttempts)
            {
                return outcome;
            }
        }
    }

    /// <summary>
    /// 一次释放尝试：占用仍指向该运行才带并发戳清掉。
    /// </summary>
    private static async ValueTask<Result> TryReleaseOnceAsync(IRepository<JobEntity, string> jobs, string job, string runId, CancellationToken cancellationToken)
    {
        var found = await jobs.FindAsync(job, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var row) || !string.Equals(row.ActiveRunId, runId, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        row.ActiveRunId = null;
        return await jobs.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 该作业阻塞中的触发器改回等待。
    /// </summary>
    private static async ValueTask<Result> UnblockAsync(IRepository<TriggerEntity, string> triggers, string job, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<TriggerEntity>()
            .Where(new Specification<TriggerEntity>(trigger => trigger.Job == job && trigger.State == BlockedTrigger))
            .OrderBy(static trigger => trigger.Id);
        var blocked = await RecordQueries.ListAllAsync(triggers, query, cancellationToken).ConfigureAwait(false);
        foreach (var trigger in blocked)
        {
            trigger.State = WaitingTrigger;
        }

        return (await RecordQueries.UpdateManyAsync(triggers, blocked, cancellationToken).ConfigureAwait(false)).ToResult();
    }

    /// <summary>
    /// 失联节点上运行中的记录改为 Lost、结束时刻为 <paramref name="now"/>。
    /// </summary>
    /// <remarks>
    /// 返回改成功的行。
    /// </remarks>
    private static async ValueTask<Result<List<JobRunEntity>>> MarkLostAsync(IRepository<JobRunEntity, string> runs, List<string> dead, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lost = new List<JobRunEntity>();
        foreach (var batch in RecordQueries.Batches(dead))
        {
            var query = EntityQuery.All<JobRunEntity>()
                .Where(new Specification<JobRunEntity>(run => run.State == RunningRun && batch.Contains(run.NodeId)))
                .OrderBy(static run => run.Id);
            var rows = await RecordQueries.ListAllAsync(runs, query, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.State = LostRun;
                row.CompletedAt = now;
            }

            var updated = await RecordQueries.UpdateManyAsync(runs, rows, cancellationToken).ConfigureAwait(false);
            if (!updated.TryGetValue(out var written))
            {
                return updated.Error!;
            }

            lost.AddRange(written);
        }

        return lost;
    }

    /// <summary>
    /// 失联节点抢占的触发器改回等待：清掉抢占者与抢占时刻，没有下一次时刻的补为 <paramref name="now"/>。
    /// </summary>
    private static async ValueTask<Result> ResetAcquiredAsync(IRepository<TriggerEntity, string> triggers, List<string> dead, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var batch in RecordQueries.Batches(dead))
        {
            // 抢占者列可空，集合按可空字符串声明才能直接写进 IN 条件
            var holders = batch.Select(static id => (string?)id).ToList();
            var query = EntityQuery.All<TriggerEntity>()
                .Where(new Specification<TriggerEntity>(trigger => trigger.State == AcquiredTrigger && holders.Contains(trigger.AcquiredBy)))
                .OrderBy(static trigger => trigger.Id);
            var rows = await RecordQueries.ListAllAsync(triggers, query, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.State = WaitingTrigger;
                row.AcquiredBy = null;
                row.AcquiredAt = null;
                row.NextFireAt ??= now;
            }

            var updated = await RecordQueries.UpdateManyAsync(triggers, rows, cancellationToken).ConfigureAwait(false);
            if (updated.IsFailure)
            {
                return updated.ToResult();
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 作业没有占用（或作业已不存在）的阻塞触发器改回等待。
    /// </summary>
    /// <remarks>
    /// 占用它们的运行已被记为 Lost 或早已结束。
    /// </remarks>
    private static async ValueTask<Result> UnblockOrphansAsync(IRepository<JobEntity, string> jobs, IRepository<TriggerEntity, string> triggers, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<TriggerEntity>()
            .Where(new Specification<TriggerEntity>(trigger => trigger.State == BlockedTrigger))
            .OrderBy(static trigger => trigger.Id);
        var blocked = await RecordQueries.ListAllAsync(triggers, query, cancellationToken).ConfigureAwait(false);
        var free = new Dictionary<string, bool>(StringComparer.Ordinal);
        var waiting = new List<TriggerEntity>(blocked.Count);
        foreach (var trigger in blocked)
        {
            if (!free.TryGetValue(trigger.Job, out var isFree))
            {
                var job = await jobs.FindAsync(trigger.Job, cancellationToken).ConfigureAwait(false);
                isFree = !job.TryGetValue(out var row) || row.ActiveRunId is null;
                free[trigger.Job] = isFree;
            }

            if (isFree)
            {
                trigger.State = WaitingTrigger;
                waiting.Add(trigger);
            }
        }

        return (await RecordQueries.UpdateManyAsync(triggers, waiting, cancellationToken).ConfigureAwait(false)).ToResult();
    }

    /// <summary>
    /// 请求取消一条运行。
    /// </summary>
    /// <remarks>
    /// 不存在或不在运行中 → 运行不存在；运行记录没有并发戳，更新冲突只可能是行刚被删，同样按不在运行中报告。
    /// </remarks>
    private static async ValueTask<Result> RequestCancelOneAsync(IRepository<JobRunEntity, string> repository, string id, CancellationToken cancellationToken)
    {
        var found = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var row) || row.State != RunningRun)
        {
            return RunNotFound(id);
        }

        row.CancelRequested = true;
        var updated = await repository.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
        if (updated.IsSuccess)
        {
            return updated;
        }

        return RecordQueries.IsRace(updated.Error!) ? RunNotFound(id) : StoreInvocation.Unavailable(updated.Error!);
    }

    /// <summary>
    /// 把查询的过滤项翻译成条件。
    /// </summary>
    /// <remarks>
    /// 作业、触发器名、节点等值，状态集合取 <c>IN</c>，计划时刻按范围比较，有无错误看错误码是否为空。
    /// </remarks>
    private static EntityQuery<JobRunEntity> Filter(JobRunQuery query)
    {
        var filtered = EntityQuery.All<JobRunEntity>();
        if (query.Job is { } job)
        {
            var name = job.Value;
            filtered = filtered.Where(new Specification<JobRunEntity>(run => run.Job == name));
        }

        if (query.States is { Count: > 0 } states)
        {
            var codes = states.Select(static state => (int)state).Distinct().ToList();
            if (codes.Count == 1)
            {
                var code = codes[0];
                filtered = filtered.Where(new Specification<JobRunEntity>(run => run.State == code));
            }
            else
            {
                filtered = filtered.Where(new Specification<JobRunEntity>(run => codes.Contains(run.State)));
            }
        }

        if (query.From is { } from)
        {
            filtered = filtered.Where(new Specification<JobRunEntity>(run => run.ScheduledAt >= from));
        }

        if (query.To is { } to)
        {
            filtered = filtered.Where(new Specification<JobRunEntity>(run => run.ScheduledAt < to));
        }

        if (query.Trigger is { } trigger)
        {
            filtered = filtered.Where(new Specification<JobRunEntity>(run => run.TriggerName == trigger));
        }

        if (query.Node is { } node)
        {
            filtered = filtered.Where(new Specification<JobRunEntity>(run => run.NodeId == node));
        }

        if (query.HasError is { } hasError)
        {
            filtered = hasError
                ? filtered.Where(new Specification<JobRunEntity>(static run => run.ErrorCode != null))
                : filtered.Where(new Specification<JobRunEntity>(static run => run.ErrorCode == null));
        }

        return filtered;
    }

    /// <summary>
    /// 作业未登记。
    /// </summary>
    private static Error JobNotFound(JobName name)
        => Error.NotFound(SchedulingErrors.JobNotFound, SchedulingMessages.JobNotFound, MessageArguments.Of(("name", name.Value)));

    /// <summary>
    /// 运行不存在或已结束。
    /// </summary>
    private static Error RunNotFound(string id)
        => Error.NotFound(SchedulingErrors.RunNotFound, SchedulingMessages.RunNotFound, MessageArguments.Of(("id", id)));
}
