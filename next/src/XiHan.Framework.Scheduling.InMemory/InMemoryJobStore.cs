// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 进程内作业存储。
/// </summary>
/// <remarks>
/// 作业、触发器、运行历史（有界，超出丢弃最旧的已结束记录）与节点都在内存里，读出来的是深拷贝。不跨实例共享（<see cref="IsShared"/> 为假）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryJobStore : IJobStore
{
    /// <summary>
    /// 保护全部表。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 作业，按名。
    /// </summary>
    private readonly SortedDictionary<string, JobRecord> _jobs = new(StringComparer.Ordinal);

    /// <summary>
    /// 触发器，按键文本（作业名/触发器名）。
    /// </summary>
    private readonly SortedDictionary<string, TriggerRecord> _triggers = new(StringComparer.Ordinal);

    /// <summary>
    /// 运行记录，按运行 ID。
    /// </summary>
    private readonly Dictionary<string, JobRunRecord> _runs = new(StringComparer.Ordinal);

    /// <summary>
    /// 运行记录的写入顺序，淘汰最旧的已结束记录用。
    /// </summary>
    private readonly List<string> _runOrder = [];

    /// <summary>
    /// 节点，按 ID。
    /// </summary>
    private readonly SortedDictionary<string, SchedulerNodeRecord> _nodes = new(StringComparer.Ordinal);

    /// <summary>
    /// 运行历史上限。
    /// </summary>
    private readonly int _maxRuns;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryJobStore(InMemorySchedulingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _maxRuns = options.MaxRuns;
    }

    /// <inheritdoc />
    public bool IsShared => false;

    /// <summary>
    /// 运行记录数。
    /// </summary>
    public int RunCount
    {
        get
        {
            lock (_lock)
            {
                return _runs.Count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<JobRecord>>> FindJobAsync(JobName name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_jobs.TryGetValue(name.Value, out var job) ? Maybe.Some(job.Clone()) : Maybe.None<JobRecord>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<JobRecord>>> ListJobsAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<JobRecord>>(_jobs.Values.Select(static job => job.Clone()).ToList()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveJobsAsync(IReadOnlyList<JobRecord> jobs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        lock (_lock)
        {
            foreach (var job in jobs)
            {
                // 占用的运行 ID 由存储自己维护：替换定义不动它
                var replaced = job.Clone();
                if (_jobs.TryGetValue(job.Name.Value, out var existing))
                {
                    replaced.ActiveRunId = existing.ActiveRunId;
                }

                _jobs[job.Name.Value] = replaced;
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteJobsAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        lock (_lock)
        {
            foreach (var name in names)
            {
                _jobs.Remove(name.Value);
                foreach (var key in _triggers.Keys.Where(key => key.StartsWith(name.Value + "/", StringComparison.Ordinal)).ToList())
                {
                    _triggers.Remove(key);
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<TriggerRecord>>> TriggersAsync(JobName? job, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyList<TriggerRecord> listed = _triggers.Values.Where(trigger => job is null || trigger.Job == job.Value).Select(static trigger => trigger.Clone()).ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveTriggersAsync(IReadOnlyList<TriggerRecord> triggers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        lock (_lock)
        {
            foreach (var trigger in triggers)
            {
                _triggers[trigger.Key.ToString()] = trigger.Clone();
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteTriggersAsync(IReadOnlyList<TriggerKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (_lock)
        {
            foreach (var key in keys)
            {
                _triggers.Remove(key.ToString());
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<TriggerRecord>>> AcquireDueAsync(DateTimeOffset dueBefore, int limit, string nodeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        lock (_lock)
        {
            // 锁内选取并改状态：同一进程里多个引擎（测试）同时抢占也不会拿到同一条
            var acquired = new List<TriggerRecord>();
            foreach (var trigger in _triggers.Values
                .Where(trigger => trigger.State == TriggerState.Waiting && !trigger.Local && trigger.NextFireAt is { } next && next <= dueBefore)
                .OrderBy(static trigger => trigger.NextFireAt)
                .ThenByDescending(static trigger => trigger.Priority)
                .ThenBy(static trigger => trigger.Key.ToString(), StringComparer.Ordinal)
                .Take(Math.Max(0, limit)))
            {
                trigger.State = TriggerState.Acquired;
                trigger.AcquiredBy = nodeId;
                trigger.AcquiredAt = now;
                acquired.Add(trigger.Clone());
            }

            return ValueTask.FromResult(Result.Success<IReadOnlyList<TriggerRecord>>(acquired));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> TryBeginRunAsync(JobRunRecord run, bool exclusive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_lock)
        {
            if (!_jobs.TryGetValue(run.Job.Value, out var job))
            {
                return ValueTask.FromResult<Result<bool>>(Error.NotFound(SchedulingErrors.JobNotFound, SchedulingMessages.JobNotFound, MessageArguments.Of(("name", run.Job.Value))));
            }

            // 独占：作业上已有未结束的运行即拒绝；结束但没清理的占用视为空闲
            if (exclusive && job.ActiveRunId is { } active && _runs.TryGetValue(active, out var current) && current.State == JobRunState.Running)
            {
                return ValueTask.FromResult(Result.Success(false));
            }

            if (!_runs.ContainsKey(run.Id) && _runs.Count >= _maxRuns && !EvictOldestFinished())
            {
                return ValueTask.FromResult<Result<bool>>(Error.Unavailable(SchedulingErrors.StoreCapacity, SchedulingMessages.StoreCapacity, MessageArguments.Of(("store", "runs"), ("limit", _maxRuns))));
            }

            if (exclusive)
            {
                job.ActiveRunId = run.Id;
            }

            Upsert(run);
            return ValueTask.FromResult(Result.Success(true));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> CompleteRunAsync(JobRunRecord run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_lock)
        {
            Upsert(run);
            if (_jobs.TryGetValue(run.Job.Value, out var job) && string.Equals(job.ActiveRunId, run.Id, StringComparison.Ordinal))
            {
                job.ActiveRunId = null;
            }

            foreach (var trigger in _triggers.Values)
            {
                if (trigger.Job == run.Job && trigger.State == TriggerState.Blocked)
                {
                    trigger.State = TriggerState.Waiting;
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunsAsync(JobRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_lock)
        {
            IReadOnlyList<JobRunRecord> listed = _runs.Values
                .Where(query.Matches)
                .OrderByDescending(static run => run.ScheduledAt)
                .ThenByDescending(static run => run.Id, StringComparer.Ordinal)
                .Skip(query.Skip)
                .Take(query.Limit)
                .Select(static run => run.Clone())
                .ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<long>> CountRunsAsync(JobRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success((long)_runs.Values.Count(query.Matches)));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunningAsync(JobName? job, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyList<JobRunRecord> listed = _runs.Values
                .Where(run => run.State == JobRunState.Running && (job is null || run.Job == job.Value))
                .OrderBy(static run => run.StartedAt)
                .ThenBy(static run => run.Id, StringComparer.Ordinal)
                .Select(static run => run.Clone())
                .ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<JobRunRecord>>> LastRunsAsync(IReadOnlyList<JobName> jobs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        lock (_lock)
        {
            var wanted = jobs.Select(static job => job.Value).ToHashSet(StringComparer.Ordinal);
            IReadOnlyList<JobRunRecord> listed = _runs.Values
                .Where(run => run.State != JobRunState.Running && run.CompletedAt is not null && wanted.Contains(run.Job.Value))
                .GroupBy(static run => run.Job.Value, StringComparer.Ordinal)
                .Select(static group => group.OrderByDescending(static run => run.CompletedAt).ThenByDescending(static run => run.Id, StringComparer.Ordinal).First().Clone())
                .OrderBy(static run => run.Job.Value, StringComparer.Ordinal)
                .ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> RequestCancelAsync(IReadOnlyList<string> runIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runIds);
        lock (_lock)
        {
            var results = new List<Result>(runIds.Count);
            foreach (var id in runIds)
            {
                if (_runs.TryGetValue(id, out var run) && run.State == JobRunState.Running)
                {
                    run.CancelRequested = true;
                    results.Add(Result.Success());
                }
                else
                {
                    results.Add(Error.NotFound(SchedulingErrors.RunNotFound, SchedulingMessages.RunNotFound, MessageArguments.Of(("id", id))));
                }
            }

            return ValueTask.FromResult<IReadOnlyList<Result>>(results);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<string>>> CancelRequestedRunsAsync(string nodeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        lock (_lock)
        {
            IReadOnlyList<string> ids = _runs.Values.Where(run => run.State == JobRunState.Running && run.CancelRequested && string.Equals(run.NodeId, nodeId, StringComparison.Ordinal)).Select(static run => run.Id).ToList();
            return ValueTask.FromResult(Result.Success(ids));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> PurgeRunsAsync(DateTimeOffset before, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var purged = 0;
            foreach (var id in _runs.Values.Where(run => run.CompletedAt is { } completed && completed < before).Select(static run => run.Id).ToList())
            {
                _runs.Remove(id);
                _runOrder.Remove(id);
                purged++;
            }

            return ValueTask.FromResult(Result.Success(purged));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> HeartbeatAsync(SchedulerNodeRecord node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        lock (_lock)
        {
            _nodes[node.Id] = node.Clone();
            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SchedulerNodeRecord>>> NodesAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<SchedulerNodeRecord>>(_nodes.Values.Select(static node => node.Clone()).ToList()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<JobRunRecord>>> RecoverNodesAsync(IReadOnlyList<string> nodeIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        lock (_lock)
        {
            var dead = nodeIds.ToHashSet(StringComparer.Ordinal);
            var lost = new List<JobRunRecord>();
            foreach (var run in _runs.Values)
            {
                if (run.State == JobRunState.Running && dead.Contains(run.NodeId))
                {
                    run.State = JobRunState.Lost;
                    run.CompletedAt = now;
                    if (_jobs.TryGetValue(run.Job.Value, out var job) && string.Equals(job.ActiveRunId, run.Id, StringComparison.Ordinal))
                    {
                        job.ActiveRunId = null;
                    }

                    lost.Add(run.Clone());
                }
            }

            foreach (var trigger in _triggers.Values)
            {
                if (trigger.State is TriggerState.Acquired && trigger.AcquiredBy is { } holder && dead.Contains(holder))
                {
                    trigger.State = TriggerState.Waiting;
                    trigger.AcquiredBy = null;
                    trigger.AcquiredAt = null;
                    trigger.NextFireAt ??= now;
                }
                else if (trigger.State == TriggerState.Blocked && !_jobs.TryGetValue(trigger.Job.Value, out var job2) || (trigger.State == TriggerState.Blocked && _jobs[trigger.Job.Value].ActiveRunId is null))
                {
                    trigger.State = TriggerState.Waiting;
                }
            }

            foreach (var id in nodeIds)
            {
                _nodes.Remove(id);
            }

            return ValueTask.FromResult(Result.Success<IReadOnlyList<JobRunRecord>>(lost.OrderBy(static run => run.Id, StringComparer.Ordinal).ToList()));
        }
    }

    /// <summary>
    /// 写入或替换运行记录，保持写入顺序。
    /// </summary>
    private void Upsert(JobRunRecord run)
    {
        if (!_runs.ContainsKey(run.Id))
        {
            _runOrder.Add(run.Id);
        }

        _runs[run.Id] = run.Clone();
    }

    /// <summary>
    /// 淘汰最旧的已结束记录。
    /// </summary>
    /// <remarks>
    /// 全部在运行中时无法淘汰。
    /// </remarks>
    private bool EvictOldestFinished()
    {
        for (var index = 0; index < _runOrder.Count; index++)
        {
            var id = _runOrder[index];
            if (_runs.TryGetValue(id, out var run) && run.State != JobRunState.Running)
            {
                _runs.Remove(id);
                _runOrder.RemoveAt(index);
                return true;
            }
        }

        return false;
    }
}
