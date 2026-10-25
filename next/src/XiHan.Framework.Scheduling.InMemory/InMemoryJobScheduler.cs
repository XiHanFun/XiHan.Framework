// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 单进程轮询调度器：每个轮询周期续约（或竞争）领导租约，把到期的作业交给 <see cref="JobDispatcher"/> 在后台运行；手动触发同步运行并等待结果。
/// 应用未进入服务状态时不按计划运行；停止时等待进行中的运行结束并释放租约。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class InMemoryJobScheduler : IJobScheduler, IDisposable
{
    /// <summary>
    /// 派发器。
    /// </summary>
    private readonly JobDispatcher _dispatcher;

    /// <summary>
    /// 领导选举。
    /// </summary>
    private readonly ILeaderElection _election;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly InMemorySchedulingOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 应用；用于判断服务状态。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 保护状态表与运行集合的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 名字 → 状态，按名字排序。
    /// </summary>
    private readonly SortedDictionary<string, JobState> _states = new(StringComparer.Ordinal);

    /// <summary>
    /// 后台运行中的任务。
    /// </summary>
    private readonly HashSet<Task> _running = [];

    /// <summary>
    /// 停止信号。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 轮询循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 轮询定时器；在启动时同步创建，让首个刻度相对启动时刻确定。
    /// </summary>
    private PeriodicTimer? _timer;

    /// <summary>
    /// 当前持有的领导租约。
    /// </summary>
    private LeaderLease? _lease;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryJobScheduler(JobRegistry registry, JobDispatcher dispatcher, ILeaderElection election, InMemorySchedulingOptions options, TimeProvider clock, App app, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _election = election ?? throw new ArgumentNullException(nameof(election));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _logger = loggerFactory.CreateLogger<InMemoryJobScheduler>();
        foreach (var definition in registry.Jobs)
        {
            _states[definition.Name.Value] = new JobState(definition);
        }
    }

    /// <summary>
    /// 是否正在运行轮询循环。
    /// </summary>
    public bool IsRunning => _loop is not null;

    /// <summary>
    /// 是否持有领导租约。
    /// </summary>
    public bool IsLeader => _lease is { IsHeld: true };

    /// <summary>
    /// 启动：按当前时刻算出各作业的下一次运行，开始轮询。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null)
        {
            return Task.CompletedTask;
        }

        var now = _clock.GetUtcNow();
        lock (_lock)
        {
            foreach (var state in _states.Values)
            {
                state.NextRun = state.Definition.Schedule.NextRun(now).Match(static next => (DateTimeOffset?)next, static () => null);
            }
        }

        _stopping = new CancellationTokenSource();
        // 定时器在这里而不是在循环任务里创建：否则时钟在循环任务开始前推进时，首个刻度会落在推进之后而丢失
        _timer = new PeriodicTimer(_options.PollInterval, _clock);
        var (timer, stopping) = (_timer, _stopping.Token);
        _loop = Task.Run(() => PollAsync(timer, stopping), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止：结束轮询，等待进行中的运行，释放租约。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loop is null || _stopping is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 轮询按取消退出
        }

        Task[] running;
        lock (_lock)
        {
            running = [.. _running];
            foreach (var state in _states.Values)
            {
                state.NextRun = null;
            }
        }

        // 进行中的运行拿的是停止信号之外的令牌，这里只等待，让作业自己收尾
        await Task.WhenAll(running).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (_lease is not null)
        {
            await _lease.ReleaseAsync().ConfigureAwait(false);
            _lease = null;
        }

        _timer?.Dispose();
        _timer = null;
        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<JobTriggerResult>> TriggerAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        var results = new List<JobTriggerResult>(names.Count);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new JobTriggerResult(name, await TriggerOneAsync(name, cancellationToken).ConfigureAwait(false)));
        }

        return results;
    }

    /// <inheritdoc />
    public Maybe<JobStatus> Status(JobName name)
    {
        lock (_lock)
        {
            return _states.TryGetValue(name.Value, out var state) ? Maybe.Some(state.Snapshot()) : default;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<JobStatus> Statuses()
    {
        lock (_lock)
        {
            return _states.Values.Select(static s => s.Snapshot()).ToList();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _stopping?.Dispose();
        _stopping = null;
    }

    /// <summary>
    /// 手动触发一个作业并等待结果。
    /// </summary>
    private async ValueTask<Result> TriggerOneAsync(JobName name, CancellationToken cancellationToken)
    {
        JobState? state;
        lock (_lock)
        {
            if (!_states.TryGetValue(name.Value, out state))
            {
                return Error.NotFound(SchedulingErrors.JobNotFound, SchedulingMessages.JobNotFound, MessageArguments.Of(("name", name.Value)));
            }

            if (state.RunningCount > 0 && !state.Definition.AllowConcurrent)
            {
                return Error.Conflict(SchedulingErrors.JobRunning, SchedulingMessages.JobRunning, MessageArguments.Of(("name", name.Value)));
            }

            state.RunningCount++;
        }

        return await RunAsync(state, JobRun.Create(name, _clock.GetUtcNow(), JobTrigger.Manual), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 轮询循环：每个周期续约或竞争租约，启动到期的作业。
    /// </summary>
    private async Task PollAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            // 宿主未进入服务状态前不按计划运行：此时特性可能尚未就绪
            if (!_app.State.IsServing())
            {
                continue;
            }

            await EnsureLeadershipAsync(cancellationToken).ConfigureAwait(false);
            var now = _clock.GetUtcNow();
            var leader = IsLeader;
            List<(JobState State, DateTimeOffset ScheduledAt)> due = [];
            lock (_lock)
            {
                foreach (var state in _states.Values)
                {
                    if (state.NextRun is not { } next || next > now || (state.Definition.LeaderOnly && !leader))
                    {
                        continue;
                    }

                    // 不允许并发且上一次未结束：跳过本次，下一次运行时刻按本次推算
                    if (state.RunningCount > 0 && !state.Definition.AllowConcurrent)
                    {
                        state.NextRun = state.Definition.Schedule.NextRun(now).Match(static n => (DateTimeOffset?)n, static () => null);
                        continue;
                    }

                    state.RunningCount++;
                    // 间隔计划在完成后再推算；cron 现在就推算，避免长作业跳过下一个刻度之外的运行
                    state.NextRun = state.Definition.Schedule.Kind == JobScheduleKind.Cron ? state.Definition.Schedule.NextRun(now).Match(static n => (DateTimeOffset?)n, static () => null) : null;
                    due.Add((state, next));
                }
            }

            foreach (var (state, scheduledAt) in due)
            {
                var run = JobRun.Create(state.Definition.Name, scheduledAt, JobTrigger.Schedule);
                Task task = null!;
                task = Task.Run(async () =>
                {
                    try
                    {
                        await RunAsync(state, run, CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _running.Remove(task);
                        }
                    }
                }, CancellationToken.None);
                lock (_lock)
                {
                    _running.Add(task);
                }
            }
        }
    }

    /// <summary>
    /// 续约；没有租约或续约失败时重新竞争。
    /// </summary>
    private async ValueTask EnsureLeadershipAsync(CancellationToken cancellationToken)
    {
        if (_lease is not null && (await _lease.RenewAsync(cancellationToken).ConfigureAwait(false)).IsSuccess)
        {
            return;
        }

        if (_lease is not null)
        {
            LogLeadershipLost(_logger, _options.LeaseResource.Value);
            _lease = null;
        }

        var acquired = await _election.TryAcquireAsync(_options.LeaseResource, _options.LeaseTimeToLive, cancellationToken).ConfigureAwait(false);
        if (acquired.TryGetValue(out var lease))
        {
            _lease = lease;
            LogLeadershipAcquired(_logger, _options.LeaseResource.Value);
        }
    }

    /// <summary>
    /// 执行一次运行并记录结果；调用方已把运行计数加一。
    /// </summary>
    private async ValueTask<Result> RunAsync(JobState state, JobRun run, CancellationToken cancellationToken)
    {
        var startedAt = _clock.GetUtcNow();
        Result result;
        try
        {
            result = await _dispatcher.DispatchAsync(run, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_lock)
            {
                state.RunningCount--;
            }

            throw;
        }

        var completedAt = _clock.GetUtcNow();
        lock (_lock)
        {
            state.RunningCount--;
            state.Last = new JobOutcome(run, startedAt, completedAt, result);
            // 间隔计划：上一次完成后再等一个间隔；只对按计划的运行推算，手动触发不改变计划
            if (run.Trigger == JobTrigger.Schedule && state.Definition.Schedule.Kind == JobScheduleKind.Interval)
            {
                state.NextRun = completedAt + state.Definition.Schedule.Interval;
            }
        }

        if (result.IsFailure)
        {
            LogRunFailed(_logger, run.Name.Value, run.Id, result.Error!.Code);
        }

        return result;
    }

    /// <summary>
    /// 取得领导租约。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired the leader lease for '{Resource}'.")]
    private static partial void LogLeadershipAcquired(ILogger logger, string resource);

    /// <summary>
    /// 失去领导租约。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Lost the leader lease for '{Resource}'; scheduled jobs pause until it is reacquired.")]
    private static partial void LogLeadershipLost(ILogger logger, string resource);

    /// <summary>
    /// 一次运行失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {Job} run {RunId} failed: {ErrorCode}")]
    private static partial void LogRunFailed(ILogger logger, string job, string runId, string errorCode);
}
