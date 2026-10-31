// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Events;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 调度引擎（RFC-0013）。
/// </summary>
/// <remarks>
/// 按周期抢占到期的触发器并在本节点运行，推算下一次、处理 misfire 与并发阻塞，记录运行历史；领导者负责失联恢复与历史清理。Provider 注册 <see cref="IJobStore"/> 与 <see cref="ILeaderElection"/> 后装配并启停它。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class JobSchedulerEngine : IJobScheduler, IDisposable
{
    /// <summary>
    /// 手动入队的一次性触发器名前缀。
    /// </summary>
    public const string ManualTriggerPrefix = "manual-";

    /// <summary>
    /// 恢复运行的一次性触发器名前缀。
    /// </summary>
    public const string RecoveryTriggerPrefix = "recovery-";

    /// <summary>
    /// 重试的一次性触发器名前缀。
    /// </summary>
    public const string RetryTriggerPrefix = "retry-";

    /// <summary>
    /// 一次性触发器的优先级：高于普通触发器，入队的运行先于到期的计划。
    /// </summary>
    private const int TransientPriority = 10;

    /// <summary>
    /// 本地触发器集合的刷新周期（按轮询次数）。
    /// </summary>
    private const int LocalRefreshTicks = 10;

    /// <summary>
    /// 存储。
    /// </summary>
    private readonly IJobStore _store;

    /// <summary>
    /// 代码登记的作业。
    /// </summary>
    private readonly JobRegistry _codeJobs;

    /// <summary>
    /// 作业类型登记表。
    /// </summary>
    private readonly JobTypeRegistry _types;

    /// <summary>
    /// 日历登记表。
    /// </summary>
    private readonly JobCalendars _calendars;

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
    private readonly SchedulerOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 应用（服务状态判定）。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 根服务提供者：按需解析消息发布者。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 保护运行表、本地触发器表与租约。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 本节点进行中的运行，按运行 ID。
    /// </summary>
    private readonly Dictionary<string, RunningRun> _running = new(StringComparer.Ordinal);

    /// <summary>
    /// 本地触发器的推算状态，按触发器键文本。
    /// </summary>
    private readonly Dictionary<string, LocalTriggerState> _local = new(StringComparer.Ordinal);

    /// <summary>
    /// 本实例等待中的重试。
    /// </summary>
    /// <remarks>
    /// 每个实例各自运行的作业的重试只在失败的实例上进行，不进存储。
    /// </remarks>
    private readonly List<PendingRetry> _localRetries = [];

    /// <summary>
    /// 轮询周期之间互斥：循环与手动 <see cref="PollOnceAsync"/> 不重叠。
    /// </summary>
    private readonly SemaphoreSlim _polling = new(1, 1);

    /// <summary>
    /// 触发器读改写的互斥。
    /// </summary>
    /// <remarks>
    /// 存储整体替换、不比对版本：抢占后运行、运行结束推算下一次、暂停与恢复、登记与删除各自读出触发器改完写回，本节点上交错就会互相覆盖（如结束时把刚暂停的写回等待）。
    /// </remarks>
    private readonly SemaphoreSlim _triggerWrites = new(1, 1);

    /// <summary>
    /// 启动时刻。
    /// </summary>
    private DateTimeOffset _startedAt;

    /// <summary>
    /// 停止信号。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 轮询循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 轮询定时器。
    /// </summary>
    private PeriodicTimer? _timer;

    /// <summary>
    /// 持有的领导租约。
    /// </summary>
    private LeaderLease? _lease;

    /// <summary>
    /// 代码作业是否已写入存储。
    /// </summary>
    private bool _synced;

    /// <summary>
    /// 本地触发器集合是否需要立即刷新。
    /// </summary>
    private bool _localDirty = true;

    /// <summary>
    /// 距上次刷新本地触发器集合的轮询次数。
    /// </summary>
    private int _ticksSinceLocalRefresh;

    /// <summary>
    /// 上次维护时刻。
    /// </summary>
    private DateTimeOffset _lastHousekeeping = DateTimeOffset.MinValue;

    /// <summary>
    /// 创建。
    /// </summary>
    public JobSchedulerEngine(IJobStore store, JobRegistry codeJobs, JobTypeRegistry types, JobCalendars calendars, JobDispatcher dispatcher, ILeaderElection election, SchedulerOptions options, TimeProvider clock, App app, IServiceProvider services, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _codeJobs = codeJobs ?? throw new ArgumentNullException(nameof(codeJobs));
        _types = types ?? throw new ArgumentNullException(nameof(types));
        _calendars = calendars ?? throw new ArgumentNullException(nameof(calendars));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _election = election ?? throw new ArgumentNullException(nameof(election));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = loggerFactory.CreateLogger<JobSchedulerEngine>();
    }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public string NodeId => _options.NodeId;

    /// <summary>
    /// 轮询是否在进行。
    /// </summary>
    public bool IsRunning => _loop is not null;

    /// <summary>
    /// 是否持有领导租约。
    /// </summary>
    public bool IsLeader => _lease is { IsHeld: true };

    /// <summary>
    /// 本节点进行中的运行数。
    /// </summary>
    public int RunningCount
    {
        get
        {
            lock (_lock)
            {
                return _running.Count;
            }
        }
    }

    /// <summary>
    /// 开始轮询。
    /// </summary>
    /// <remarks>
    /// 幂等。
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null)
        {
            return Task.CompletedTask;
        }

        _startedAt = _clock.GetUtcNow();
        _stopping = new CancellationTokenSource();
        // 定时器在这里而不是在循环任务里创建：否则时钟在循环任务开始前推进时，首个刻度会落在推进之后而丢失
        _timer = new PeriodicTimer(_options.PollInterval, _clock);
        var (timer, stopping) = (_timer, _stopping.Token);
        _loop = Task.Run(() => LoopAsync(timer, stopping), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止轮询。
    /// </summary>
    /// <remarks>
    /// 等待进行中的运行结束；<paramref name="cancellationToken"/> 触发时取消它们并再等待收尾；最后释放领导租约。
    /// </remarks>
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
            running = _running.Values.Select(static run => run.Task).ToArray();
        }

        try
        {
            await Task.WhenAll(running).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 宿主不再等待：取消进行中的运行，再给它们收尾的机会（不受取消令牌约束，作业自己决定多快退出）
            CancellationTokenSource[] cancellations;
            lock (_lock)
            {
                cancellations = _running.Values.Select(static run => run.Cancellation).ToArray();
            }

            foreach (var cancellation in cancellations)
            {
                await cancellation.CancelAsync().ConfigureAwait(false);
            }

            await Task.WhenAll(running).ConfigureAwait(false);
        }

        if (_lease is not null)
        {
            await _lease.ReleaseAsync().ConfigureAwait(false);
            _lease = null;
        }

        lock (_lock)
        {
            // 本实例未到期的重试随进程结束（RFC-0013）
            _localRetries.Clear();
        }

        _timer?.Dispose();
        _timer = null;
        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <summary>
    /// 执行一个轮询周期（心跳、领导、维护、取消、本地触发器、抢占）。
    /// </summary>
    /// <remarks>
    /// 测试与套件用，与循环互斥。
    /// </remarks>
    public async ValueTask PollOnceAsync(CancellationToken cancellationToken)
    {
        await _polling.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await TickAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _polling.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> ScheduleAsync(IReadOnlyList<JobDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        // 先把代码作业写进存储：首个轮询周期之前的修改也按代码作业判定来源，不被随后的同步冲掉
        await EnsureSyncedAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<Result>(definitions.Count);
        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ScheduleOneAsync(definition, JobSource.Runtime, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> RestoreAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        // 先把代码作业写进存储：首个轮询周期之前的修改也按代码作业判定来源，不被随后的同步冲掉
        await EnsureSyncedAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<Result>(names.Count);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(_codeJobs.Find(name).TryGetValue(out var definition)
                ? await ScheduleOneAsync(definition, JobSource.Code, cancellationToken, force: true).ConfigureAwait(false)
                : Error.NotFound(SchedulingErrors.CodeDefinitionNotFound, SchedulingMessages.CodeDefinitionNotFound, MessageArguments.Of(("name", name.Value))));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> UnscheduleAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        // 先把代码作业写进存储：首个轮询周期之前的修改也按代码作业判定来源，不被随后的同步冲掉
        await EnsureSyncedAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<Result>(names.Count);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = await _store.FindJobAsync(name, cancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var maybe))
            {
                results.Add(found.Error!);
                continue;
            }

            if (!maybe.HasValue)
            {
                results.Add(JobNotFound(name));
                continue;
            }

            var deleted = await UnderTriggerLockAsync(() => _store.DeleteJobsAsync([name], cancellationToken), cancellationToken).ConfigureAwait(false);
            results.Add(deleted);
            if (deleted.IsSuccess)
            {
                ForgetLocal(name);
                await PublishAsync(SchedulingTopics.JobUnscheduled, new SchedulingEventData { Job = name.Value, NodeId = NodeId, At = _clock.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PauseAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken)
        => SetPausedAsync(names, paused: true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> ResumeAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken)
        => SetPausedAsync(names, paused: false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PauseTriggersAsync(IReadOnlyList<TriggerKey> keys, CancellationToken cancellationToken)
        => SetTriggersPausedAsync(keys, paused: true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> ResumeTriggersAsync(IReadOnlyList<TriggerKey> keys, CancellationToken cancellationToken)
        => SetTriggersPausedAsync(keys, paused: false, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<JobTriggerResult>> TriggerAsync(IReadOnlyList<JobTriggerRequest> requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var results = new List<JobTriggerResult>(requests.Count);
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new JobTriggerResult(request.Name, await TriggerOneAsync(request, cancellationToken).ConfigureAwait(false)));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<string>>> EnqueueAsync(IReadOnlyList<JobTriggerRequest> requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var results = new List<Result<string>>(requests.Count);
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = await ResolveJobAsync(request.Name, cancellationToken).ConfigureAwait(false);
            if (!resolved.TryGetValue(out var job))
            {
                results.Add(resolved.Error!);
                continue;
            }

            var trigger = TransientTrigger(job.Record.Name, ManualTriggerPrefix + JobRun.NewId(), JobTrigger.Manual, request.Data, _clock.GetUtcNow());
            var saved = await _store.SaveTriggersAsync([trigger], cancellationToken).ConfigureAwait(false);
            results.Add(saved.IsSuccess ? Result.Success(trigger.Name) : saved.Error!);
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> CancelRunsAsync(IReadOnlyList<string> runIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runIds);
        var results = new List<Result>(runIds.Count);
        foreach (var runId in runIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunningRun? local;
            lock (_lock)
            {
                _running.TryGetValue(runId, out local);
            }

            if (local is not null)
            {
                // 本节点的运行：直接取消令牌，不必绕存储
                await local.Cancellation.CancelAsync().ConfigureAwait(false);
                results.Add(Result.Success());
                continue;
            }

            var requested = await _store.RequestCancelAsync([runId], cancellationToken).ConfigureAwait(false);
            results.Add(requested[0]);
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<JobStatus>>> StatusAsync(JobName name, CancellationToken cancellationToken)
    {
        var found = await FindJobRecordAsync(name, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var job))
        {
            return Maybe.None<JobStatus>();
        }

        var triggers = await _store.TriggersAsync(name, cancellationToken).ConfigureAwait(false);
        if (!triggers.TryGetValue(out var triggerList))
        {
            return triggers.Error!;
        }

        var running = await _store.RunningAsync(name, cancellationToken).ConfigureAwait(false);
        if (!running.TryGetValue(out var runningList))
        {
            return running.Error!;
        }

        var last = await _store.LastRunsAsync([name], cancellationToken).ConfigureAwait(false);
        if (!last.TryGetValue(out var lastList))
        {
            return last.Error!;
        }

        return Maybe.Some(BuildStatus(job, triggerList, runningList.Count, lastList.Count > 0 ? lastList[0] : null));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<JobStatus>>> StatusesAsync(CancellationToken cancellationToken)
    {
        var jobs = await _store.ListJobsAsync(cancellationToken).ConfigureAwait(false);
        if (!jobs.TryGetValue(out var jobList))
        {
            return jobs.Error!;
        }

        var triggers = await _store.TriggersAsync(null, cancellationToken).ConfigureAwait(false);
        if (!triggers.TryGetValue(out var triggerList))
        {
            return triggers.Error!;
        }

        var running = await _store.RunningAsync(null, cancellationToken).ConfigureAwait(false);
        if (!running.TryGetValue(out var runningList))
        {
            return running.Error!;
        }

        var last = await _store.LastRunsAsync(jobList.Select(static job => job.Name).ToList(), cancellationToken).ConfigureAwait(false);
        if (!last.TryGetValue(out var lastList))
        {
            return last.Error!;
        }

        // 尚未同步进存储的代码作业也算在内（没有触发器状态，手动可触发）
        var known = jobList.Select(static job => job.Name).ToHashSet();
        var pending = new List<JobRecord>();
        foreach (var definition in _codeJobs.Jobs)
        {
            if (!known.Contains(definition.Name) && (await FindJobRecordAsync(definition.Name, cancellationToken).ConfigureAwait(false)).TryGetValue(out var maybe) && maybe.TryGetValue(out var record))
            {
                pending.Add(record);
            }
        }

        var triggersByJob = triggerList.ToLookup(static trigger => trigger.Job);
        var runningByJob = runningList.ToLookup(static run => run.Job);
        var lastByJob = lastList.ToDictionary(static run => run.Job);
        IReadOnlyList<JobStatus> statuses = jobList.Concat(pending)
            .OrderBy(static job => job.Name.Value, StringComparer.Ordinal)
            .Select(job => BuildStatus(job, triggersByJob[job.Name].ToList(), runningByJob[job.Name].Count(), lastByJob.GetValueOrDefault(job.Name)))
            .ToList();
        return Result.Success(statuses);
    }

    /// <inheritdoc />
    public ValueTask<Result<long>> CountRunsAsync(JobRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _store.CountRunsAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<JobRunRecord>>> RunsAsync(JobRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _store.RunsAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _stopping?.Dispose();
        _stopping = null;
        _polling.Dispose();
        _triggerWrites.Dispose();
    }

    /// <summary>
    /// 轮询循环：每个刻度执行一个周期，单次周期的意外不停掉循环。
    /// </summary>
    private async Task LoopAsync(PeriodicTimer timer, CancellationToken stopping)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stopping).ConfigureAwait(false))
            {
                await _polling.WaitAsync(stopping).ConfigureAwait(false);
                try
                {
                    await TickAsync(stopping).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    LogTickThrew(_logger, exception);
                }
                finally
                {
                    _polling.Release();
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // 停止
        }
    }

    /// <summary>
    /// 一个周期。
    /// </summary>
    private async ValueTask TickAsync(CancellationToken cancellationToken)
    {
        // 宿主未进入服务状态前不按计划运行：此时特性可能尚未就绪，数据库存储也无法经管道访问
        if (!_app.State.IsServing())
        {
            return;
        }

        if (!await EnsureSyncedAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var now = _clock.GetUtcNow();
        if (_store.IsShared)
        {
            var beat = await _store.HeartbeatAsync(new SchedulerNodeRecord { Id = NodeId, StartedAt = _startedAt, LastHeartbeatAt = now }, cancellationToken).ConfigureAwait(false);
            if (beat.IsFailure)
            {
                LogStoreFailed(_logger, "heartbeat", beat.Error!.Code);
            }
        }

        await EnsureLeadershipAsync(cancellationToken).ConfigureAwait(false);
        if (IsLeader && now - _lastHousekeeping >= _options.HousekeepingInterval)
        {
            _lastHousekeeping = now;
            await HousekeepAsync(now, cancellationToken).ConfigureAwait(false);
        }

        await ApplyCancelRequestsAsync(cancellationToken).ConfigureAwait(false);
        await RefreshLocalIfDueAsync(now, cancellationToken).ConfigureAwait(false);
        await FireLocalTriggersAsync(now, cancellationToken).ConfigureAwait(false);
        await FireLocalRetriesAsync(now, cancellationToken).ConfigureAwait(false);
        await AcquireAndFireAsync(now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 首次把代码登记的作业写入存储。
    /// </summary>
    /// <remarks>
    /// 失败时记日志并在下个周期重试。
    /// </remarks>
    private async ValueTask<bool> EnsureSyncedAsync(CancellationToken cancellationToken)
    {
        if (_synced)
        {
            return true;
        }

        foreach (var definition in _codeJobs.Jobs)
        {
            var scheduled = await ScheduleOneAsync(definition, JobSource.Code, cancellationToken).ConfigureAwait(false);
            if (scheduled.IsFailure)
            {
                LogSyncFailed(_logger, definition.Name.Value, scheduled.Error!.Code);
                return false;
            }
        }

        _synced = true;
        return true;
    }

    /// <summary>
    /// 续约或获取领导租约。
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
    /// 领导者维护：失联节点恢复、历史清理。
    /// </summary>
    private async ValueTask HousekeepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_store.IsShared)
        {
            var nodes = await _store.NodesAsync(cancellationToken).ConfigureAwait(false);
            if (nodes.TryGetValue(out var nodeList))
            {
                var dead = nodeList.Where(node => !string.Equals(node.Id, NodeId, StringComparison.Ordinal) && node.LastHeartbeatAt + _options.NodeTimeout < now).Select(static node => node.Id).ToList();
                if (dead.Count > 0)
                {
                    await RecoverAsync(dead, now, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                LogStoreFailed(_logger, "nodes", nodes.Error!.Code);
            }
        }

        if (_options.HistoryRetention > TimeSpan.Zero)
        {
            var purged = await _store.PurgeRunsAsync(now - _options.HistoryRetention, cancellationToken).ConfigureAwait(false);
            if (purged.IsFailure)
            {
                LogStoreFailed(_logger, "purge", purged.Error!.Code);
            }
        }
    }

    /// <summary>
    /// 恢复失联节点：它们的运行记为 Lost，要求恢复的作业以恢复触发器重新入队。
    /// </summary>
    private async ValueTask RecoverAsync(IReadOnlyList<string> nodeIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        LogNodesLost(_logger, string.Join(", ", nodeIds));
        var recovered = await _store.RecoverNodesAsync(nodeIds, now, cancellationToken).ConfigureAwait(false);
        if (!recovered.TryGetValue(out var lost))
        {
            LogStoreFailed(_logger, "recover", recovered.Error!.Code);
            return;
        }

        foreach (var run in lost)
        {
            await PublishAsync(SchedulingTopics.RunLost, EventOf(run), cancellationToken).ConfigureAwait(false);
            var job = await _store.FindJobAsync(run.Job, cancellationToken).ConfigureAwait(false);
            if (job.TryGetValue(out var maybe) && maybe.TryGetValue(out var record) && record.RequestsRecovery)
            {
                var trigger = TransientTrigger(run.Job, RecoveryTriggerPrefix + run.Id, JobTrigger.Recovery, run.Data, now);
                var saved = await _store.SaveTriggersAsync([trigger], cancellationToken).ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    LogStoreFailed(_logger, "recovery-trigger", saved.Error!.Code);
                }
            }
        }
    }

    /// <summary>
    /// 取消本节点被请求取消的运行。
    /// </summary>
    private async ValueTask ApplyCancelRequestsAsync(CancellationToken cancellationToken)
    {
        if (RunningCount == 0)
        {
            return;
        }

        var requested = await _store.CancelRequestedRunsAsync(NodeId, cancellationToken).ConfigureAwait(false);
        if (!requested.TryGetValue(out var ids))
        {
            LogStoreFailed(_logger, "cancel-requests", requested.Error!.Code);
            return;
        }

        foreach (var id in ids)
        {
            RunningRun? run;
            lock (_lock)
            {
                _running.TryGetValue(id, out run);
            }

            if (run is not null)
            {
                await run.Cancellation.CancelAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 到了刷新周期或有变更时重建本地触发器集合。
    /// </summary>
    private async ValueTask RefreshLocalIfDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!_localDirty && ++_ticksSinceLocalRefresh < LocalRefreshTicks)
        {
            return;
        }

        var jobs = await _store.ListJobsAsync(cancellationToken).ConfigureAwait(false);
        var triggers = await _store.TriggersAsync(null, cancellationToken).ConfigureAwait(false);
        if (!jobs.TryGetValue(out var jobList) || !triggers.TryGetValue(out var triggerList))
        {
            LogStoreFailed(_logger, "local-refresh", (jobs.Error ?? triggers.Error)!.Code);
            return;
        }

        _localDirty = false;
        _ticksSinceLocalRefresh = 0;
        var jobsByName = jobList.ToDictionary(static job => job.Name);
        lock (_lock)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trigger in triggerList)
            {
                if (!trigger.Local || !jobsByName.TryGetValue(trigger.Job, out var job) || job.Paused || trigger.State is TriggerState.Paused or TriggerState.Complete or TriggerState.Error)
                {
                    continue;
                }

                var key = trigger.Key.ToString();
                seen.Add(key);
                if (_local.TryGetValue(key, out var state))
                {
                    // 计划变了才重新推算，否则保留本节点的进度（下一次时刻与次数）
                    var changed = !string.Equals(state.Trigger.Schedule.ToString(), trigger.Schedule.ToString(), StringComparison.Ordinal);
                    state.Trigger = trigger;
                    state.Job = job;
                    if (changed)
                    {
                        state.FireCount = trigger.FireCount;
                        state.NextFireAt = NextFire(trigger.Schedule, now, state.FireCount);
                    }

                    continue;
                }

                _local[key] = new LocalTriggerState(trigger, job, NextFire(trigger.Schedule, now, trigger.FireCount));
            }

            foreach (var key in _local.Keys.ToList())
            {
                if (!seen.Contains(key))
                {
                    _local.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// 运行到期的本地触发器。
    /// </summary>
    private async ValueTask FireLocalTriggersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<LocalTriggerState> due;
        lock (_lock)
        {
            due = _local.Values.Where(state => state.NextFireAt is { } next && next <= now).OrderBy(static state => state.NextFireAt).ThenByDescending(static state => state.Trigger.Priority).ToList();
        }

        foreach (var state in due)
        {
            var planned = state.NextFireAt!.Value;
            if (!_types.Find(state.Job.JobTypeName).TryGetValue(out var jobType))
            {
                lock (_lock)
                {
                    state.NextFireAt = null;
                }

                LogTypeMissing(_logger, state.Job.Name.Value, state.Job.JobTypeName);
                continue;
            }

            // 不允许并发且本节点上一次未结束：跳过本次，下一次按现在推算
            if (!state.Job.AllowConcurrent && state.Running > 0)
            {
                lock (_lock)
                {
                    state.NextFireAt = NextFire(state.Trigger.Schedule, now, state.FireCount);
                }

                continue;
            }

            var (run, scheduledAt) = Plan(state.Trigger, planned, now, out var skip);
            if (skip)
            {
                lock (_lock)
                {
                    state.NextFireAt = NextFire(state.Trigger.Schedule, now, state.FireCount);
                }

                continue;
            }

            var jobRun = JobRun.Create(state.Job.Name, scheduledAt, state.Trigger.RunAs, state.Trigger.Name, Merge(state.Job.Data, state.Trigger.Data, null));
            var record = NewRecord(jobRun, now);
            var begun = await _store.TryBeginRunAsync(record, exclusive: false, cancellationToken).ConfigureAwait(false);
            if (!begun.TryGetValue(out _))
            {
                LogStoreFailed(_logger, "begin-run", begun.Error!.Code);
                continue;
            }

            lock (_lock)
            {
                state.Running++;
                state.FireCount++;
                state.PreviousFireAt = scheduledAt;
                state.NextFireAt = state.Trigger.Schedule.Kind == JobScheduleKind.Interval ? null : NextFire(state.Trigger.Schedule, run, state.FireCount);
            }

            StartBackground(record, jobRun, jobType, state.Job, completedAt =>
            {
                lock (_lock)
                {
                    state.Running--;
                    if (state.Trigger.Schedule.Kind == JobScheduleKind.Interval)
                    {
                        state.NextFireAt = NextFire(state.Trigger.Schedule, completedAt, state.FireCount);
                    }
                }

                return ValueTask.CompletedTask;
            });
        }
    }

    /// <summary>
    /// 抢占到期的触发器并运行。
    /// </summary>
    private async ValueTask AcquireAndFireAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // 不共享的存储（进程内）：集群唯一的作业只在领导者上按计划运行，否则多实例各跑一份；入队的一次性运行只有本进程看得到，不受领导限制
        var scheduledAllowed = _store.IsShared || IsLeader;
        var capacity = Math.Min(_options.AcquireBatchSize, _options.MaxConcurrentRuns - RunningCount);
        if (capacity <= 0)
        {
            return;
        }

        var acquired = await _store.AcquireDueAsync(now, capacity, NodeId, now, cancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var triggers))
        {
            LogStoreFailed(_logger, "acquire", acquired.Error!.Code);
            return;
        }

        if (triggers.Count == 0)
        {
            return;
        }

        foreach (var trigger in triggers)
        {
            await UnderTriggerLockAsync(() => FireAcquiredAsync(trigger.Key, scheduledAllowed, now, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 在锁内以存储里的当前状态运行一条刚抢占的触发器。
    /// </summary>
    /// <remarks>
    /// 抢占之后、拿到锁之前，暂停、改计划或删除可能已经改了它：不再由本节点持有就不动，作业没了就删掉孤儿。
    /// </remarks>
    private async ValueTask FireAcquiredAsync(TriggerKey key, bool scheduledAllowed, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var found = await _store.FindJobAsync(key.Job, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe) || !maybe.TryGetValue(out var job))
        {
            // 作业没了：触发器是孤儿，删掉
            await _store.DeleteTriggersAsync([key], cancellationToken).ConfigureAwait(false);
            return;
        }

        var current = await _store.TriggersAsync(key.Job, cancellationToken).ConfigureAwait(false);
        if (!current.TryGetValue(out var triggers))
        {
            LogStoreFailed(_logger, "fire-trigger", current.Error!.Code);
            return;
        }

        var trigger = triggers.FirstOrDefault(candidate => string.Equals(candidate.Name, key.Trigger, StringComparison.Ordinal));
        if (trigger is null || trigger.State != TriggerState.Acquired || !string.Equals(trigger.AcquiredBy, NodeId, StringComparison.Ordinal))
        {
            return;
        }

        if (!scheduledAllowed && !trigger.Transient)
        {
            // 没有领导权：按计划的触发器放回等待，等领导权（或换成共享存储）再运行
            trigger.State = TriggerState.Waiting;
            trigger.AcquiredBy = null;
            trigger.AcquiredAt = null;
            await SaveTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);
            return;
        }

        await FireAcquiredAsync(trigger, job, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 运行一条已抢占的触发器。
    /// </summary>
    /// <remarks>
    /// 暂停与类型检查、misfire、独占开始、推算下一次、后台运行。
    /// </remarks>
    private async ValueTask FireAcquiredAsync(TriggerRecord trigger, JobRecord job, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (job.Paused)
        {
            trigger.State = TriggerState.Paused;
            trigger.AcquiredBy = null;
            trigger.AcquiredAt = null;
            await SaveTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!_types.Find(job.JobTypeName).TryGetValue(out var jobType))
        {
            trigger.State = TriggerState.Error;
            trigger.Error = $"Job type '{job.JobTypeName}' is not registered.";
            trigger.AcquiredBy = null;
            trigger.AcquiredAt = null;
            LogTypeMissing(_logger, job.Name.Value, job.JobTypeName);
            await SaveTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);
            return;
        }

        var planned = trigger.NextFireAt ?? now;
        var (runFrom, scheduledAt) = Plan(trigger, planned, now, out var skip);
        if (skip)
        {
            ReleaseTrigger(trigger, NextFire(trigger.Schedule, now, trigger.FireCount));
            await SaveTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);
            return;
        }

        var run = JobRun.Create(job.Name, scheduledAt, trigger.RunAs, trigger.Transient ? null : trigger.Name, Merge(job.Data, trigger.Data, null)) with { Attempt = trigger.Attempt, RetryOf = trigger.RetryOf };
        var record = NewRecord(run, now);
        var begun = await _store.TryBeginRunAsync(record, exclusive: !job.AllowConcurrent, cancellationToken).ConfigureAwait(false);
        if (!begun.TryGetValue(out var started))
        {
            LogStoreFailed(_logger, "begin-run", begun.Error!.Code);
            trigger.State = TriggerState.Waiting;
            trigger.AcquiredBy = null;
            trigger.AcquiredAt = null;
            await SaveTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!started)
        {
            // 上一次未结束：阻塞到它结束，存储在结束时把它改回等待，到期时刻不变，届时按 misfire 策略处理；
            // 确认占用与写入阻塞由存储一次完成，上一次恰在此时结束则直接回到等待，不会阻塞在一个再也不会结束的运行上
            var blocked = await _store.BlockTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);
            if (blocked.IsFailure)
            {
                LogStoreFailed(_logger, "block-trigger", blocked.Error!.Code);
            }

            return;
        }

        trigger.FireCount++;
        trigger.PreviousFireAt = scheduledAt;
        var interval = trigger.Schedule.Kind == JobScheduleKind.Interval;
        if (interval)
        {
            // 间隔计划：完成后再推算下一次；运行期间保持已抢占，失联时由恢复改回等待
            trigger.NextFireAt = null;
        }
        else if (trigger.Transient)
        {
            await _store.DeleteTriggersAsync([trigger.Key], cancellationToken).ConfigureAwait(false);
        }
        else
        {
            ReleaseTrigger(trigger, NextFire(trigger.Schedule, runFrom, trigger.FireCount));
        }

        if (!trigger.Transient)
        {
            await SaveTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);
        }

        StartBackground(
            record,
            run,
            jobType,
            job,
            interval ? completedAt => FinishIntervalTriggerAsync(trigger, completedAt) : null,
            interval ? completedAt => ReleaseIntervalTriggerAsync(trigger, completedAt) : null);
    }

    /// <summary>
    /// 间隔触发器在运行结束时写下一次时刻。
    /// </summary>
    /// <remarks>
    /// 在运行标为完成之前执行：触发器仍由本节点持有，状态里已能看到下一次。重读存储里的状态，只在仍由本节点持有时写回；被暂停的只更新时刻、保持暂停。
    /// </remarks>
    private ValueTask FinishIntervalTriggerAsync(TriggerRecord trigger, DateTimeOffset completedAt)
        => UnderTriggerLockAsync(() => FinishIntervalTriggerLockedAsync(trigger, completedAt), CancellationToken.None);

    /// <summary>
    /// 在锁内写间隔触发器的下一次时刻。
    /// </summary>
    private async ValueTask FinishIntervalTriggerLockedAsync(TriggerRecord trigger, DateTimeOffset completedAt)
    {
        var fresh = await FreshTriggerAsync(trigger, "finish-trigger").ConfigureAwait(false);
        if (fresh is null)
        {
            return;
        }

        if (fresh.State == TriggerState.Paused)
        {
            fresh.AcquiredBy = null;
            fresh.AcquiredAt = null;
        }
        else if (!IsHeldHere(fresh))
        {
            return;
        }

        fresh.NextFireAt = NextFire(fresh.Schedule, completedAt, fresh.FireCount);
        await SaveTriggerAsync(fresh, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 间隔触发器在运行标为完成之后放回等待。
    /// </summary>
    /// <remarks>
    /// 占用此时已释放：别的节点不会在本次运行结束前抢到它、再因占用未释放把它阻塞。重读存储里的状态，只在仍由本节点持有时写回；下一次按同一结束时刻推算，没有下一次即完成（一次性的删除）。
    /// </remarks>
    private ValueTask ReleaseIntervalTriggerAsync(TriggerRecord trigger, DateTimeOffset completedAt)
        => UnderTriggerLockAsync(() => ReleaseIntervalTriggerLockedAsync(trigger, completedAt), CancellationToken.None);

    /// <summary>
    /// 在锁内放回间隔触发器。
    /// </summary>
    private async ValueTask ReleaseIntervalTriggerLockedAsync(TriggerRecord trigger, DateTimeOffset completedAt)
    {
        var fresh = await FreshTriggerAsync(trigger, "release-trigger").ConfigureAwait(false);
        if (fresh is null || !IsHeldHere(fresh))
        {
            return;
        }

        ReleaseTrigger(fresh, NextFire(fresh.Schedule, completedAt, fresh.FireCount));
        if (fresh.Transient && fresh.State == TriggerState.Complete)
        {
            await _store.DeleteTriggersAsync([fresh.Key], CancellationToken.None).ConfigureAwait(false);
            return;
        }

        await SaveTriggerAsync(fresh, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 重读一条触发器在存储里的当前状态。
    /// </summary>
    /// <remarks>
    /// 读失败记日志；已被删除为 <see langword="null"/>。
    /// </remarks>
    private async ValueTask<TriggerRecord?> FreshTriggerAsync(TriggerRecord trigger, string operation)
    {
        var found = await _store.TriggersAsync(trigger.Job, CancellationToken.None).ConfigureAwait(false);
        if (!found.TryGetValue(out var triggers))
        {
            LogStoreFailed(_logger, operation, found.Error!.Code);
            return null;
        }

        return triggers.FirstOrDefault(candidate => string.Equals(candidate.Name, trigger.Name, StringComparison.Ordinal));
    }

    /// <summary>
    /// 触发器仍由本节点抢占持有。
    /// </summary>
    private bool IsHeldHere(TriggerRecord trigger)
        => trigger.State == TriggerState.Acquired && string.Equals(trigger.AcquiredBy, NodeId, StringComparison.Ordinal);

    /// <summary>
    /// 后台运行。
    /// </summary>
    /// <remarks>
    /// 运行标为完成之前执行 <paramref name="completed"/>，之后执行 <paramref name="released"/>。
    /// </remarks>
    private void StartBackground(JobRunRecord record, JobRun run, Type jobType, JobRecord job, Func<DateTimeOffset, ValueTask>? completed, Func<DateTimeOffset, ValueTask>? released = null)
    {
        var cancellation = new CancellationTokenSource();
        // 两段收尾各只执行一次：正常在 ExecuteAsync 里运行标为完成的前后，执行中途抛出时在这里补上
        var running = new RunningRun(record, cancellation) { Finish = Once(completed, run), Release = Once(released, run) };
        lock (_lock)
        {
            _running[record.Id] = running;
        }

        running.Task = Task.Run(async () =>
        {
            try
            {
                await ExecuteAsync(running, run, jobType, job, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 被取消的运行已记录
            }
            catch (Exception exception)
            {
                LogRunThrew(_logger, exception, run.Name.Value, run.Id);
            }
            finally
            {
                var now = _clock.GetUtcNow();
                if (running.Finish is { } finish)
                {
                    await finish(now).ConfigureAwait(false);
                }

                if (running.Release is { } release)
                {
                    await release(now).ConfigureAwait(false);
                }

                Forget(record.Id);
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// 包成只执行一次的收尾。
    /// </summary>
    /// <remarks>
    /// 抛出的异常记日志，不影响运行的结果。
    /// </remarks>
    private Func<DateTimeOffset, ValueTask>? Once(Func<DateTimeOffset, ValueTask>? action, JobRun run)
    {
        if (action is null)
        {
            return null;
        }

        var done = 0;
        return async completedAt =>
        {
            if (Interlocked.Exchange(ref done, 1) != 0)
            {
                return;
            }

            try
            {
                await action(completedAt).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogRunThrew(_logger, exception, run.Name.Value, run.Id);
            }
        };
    }

    /// <summary>
    /// 手动触发一项：找作业与类型、独占开始、在本实例运行并等待。
    /// </summary>
    private async ValueTask<Result> TriggerOneAsync(JobTriggerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureSyncedAsync(cancellationToken).ConfigureAwait(false);
        var resolved = await ResolveJobAsync(request.Name, cancellationToken).ConfigureAwait(false);
        if (!resolved.TryGetValue(out var job))
        {
            return resolved.Error!;
        }

        var now = _clock.GetUtcNow();
        var run = JobRun.Create(job.Record.Name, now, JobTrigger.Manual, null, Merge(job.Record.Data, null, request.Data));
        var record = NewRecord(run, now);
        var begun = await _store.TryBeginRunAsync(record, exclusive: !job.Record.AllowConcurrent, cancellationToken).ConfigureAwait(false);
        if (!begun.TryGetValue(out var started))
        {
            return begun.Error!;
        }

        if (!started)
        {
            return Error.Conflict(SchedulingErrors.JobRunning, SchedulingMessages.JobRunning, MessageArguments.Of(("name", job.Record.Name.Value)));
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = new RunningRun(record, cancellation);
        lock (_lock)
        {
            _running[record.Id] = running;
        }

        try
        {
            return await ExecuteAsync(running, run, job.Type, job.Record, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Forget(record.Id);
        }
    }

    /// <summary>
    /// 运行连同收尾回调都结束后才从运行表移除：停止时等待的是整个任务。
    /// </summary>
    private void Forget(string runId)
    {
        lock (_lock)
        {
            _running.Remove(runId);
        }
    }

    /// <summary>
    /// 执行一次运行并写回结果。
    /// </summary>
    /// <remarks>
    /// 调用方的令牌取消时抛出，其他取消（取消请求、宿主停止）记为取消的失败结果；作业返回之前执行时限已过记为超时失败。失败（不含取消）按作业的重试策略排下一次尝试（ADR-0060）。
    /// </remarks>
    private async ValueTask<Result> ExecuteAsync(RunningRun running, JobRun run, Type jobType, JobRecord job, CancellationToken callerToken)
    {
        var record = running.Record;
        await PublishAsync(SchedulingTopics.RunStarted, EventOf(record), CancellationToken.None).ConfigureAwait(false);
        // 执行时限是另一个按时钟取消的源，与取消请求合成作业拿到的令牌
        using var deadline = job.Timeout is { } limit ? new CancellationTokenSource(limit, _clock) : null;
        using var linked = deadline is null ? null : CancellationTokenSource.CreateLinkedTokenSource(running.Cancellation.Token, deadline.Token);
        var token = linked?.Token ?? running.Cancellation.Token;
        Result? returned = null;
        var expired = false;
        try
        {
            returned = await _dispatcher.DispatchAsync(run, jobType, token).ConfigureAwait(false);
            expired = deadline?.IsCancellationRequested == true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 取消请求或超时，下面归类
            expired = deadline?.IsCancellationRequested == true;
        }
        finally
        {
            running.Cancellation.Dispose();
        }

        // 管道把取消翻译成失败结果（KERNEL_CANCELLED_001）而不是抛出：取消请求在先且没有成功即视为取消，其次是超时
        var canceled = running.Cancellation.IsCancellationRequested && returned is not { IsSuccess: true };
        Result result;
        if (canceled)
        {
            result = Error.Conflict(SchedulingErrors.RunCanceled, SchedulingMessages.RunCanceled, MessageArguments.Of(("id", run.Id)));
        }
        else if (expired)
        {
            result = new Error(SchedulingErrors.RunTimedOut, SchedulingMessages.RunTimedOut, MessageArguments.Of(("id", run.Id), ("timeout", job.Timeout)));
        }
        else
        {
            result = returned!.Value;
        }

        record.CompletedAt = _clock.GetUtcNow();
        record.State = canceled ? JobRunState.Canceled : result.IsSuccess ? JobRunState.Succeeded : JobRunState.Failed;
        record.ErrorCode = result.Error?.Code;
        record.ErrorMessage = result.Error?.Message;
        // 收尾在标为完成之前：间隔触发器先有下一次运行时刻，状态里不会出现「已不在运行、也没有下一次」的空档
        if (running.Finish is { } finish)
        {
            await finish(record.CompletedAt.Value).ConfigureAwait(false);
        }

        var completed = await _store.CompleteRunAsync(record, CancellationToken.None).ConfigureAwait(false);
        if (completed.IsFailure)
        {
            LogStoreFailed(_logger, "complete-run", completed.Error!.Code);
        }

        // 标为完成、占用释放之后间隔触发器才回到等待
        if (running.Release is { } release)
        {
            await release(record.CompletedAt.Value).ConfigureAwait(false);
        }

        if (result.IsFailure && !canceled)
        {
            LogRunFailed(_logger, run.Name.Value, run.Id, result.Error!.Code);
        }

        await PublishAsync(canceled ? SchedulingTopics.RunCanceled : result.IsSuccess ? SchedulingTopics.RunSucceeded : SchedulingTopics.RunFailed, EventOf(record), CancellationToken.None).ConfigureAwait(false);
        if (record.State == JobRunState.Failed && job.Retry is { } policy && run.Attempt <= policy.MaxRetries)
        {
            await ScheduleRetryAsync(job, run, policy, record.CompletedAt.Value).ConfigureAwait(false);
        }

        if (canceled && callerToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(callerToken);
        }

        return result;
    }

    /// <summary>
    /// 按重试策略排下一次尝试。
    /// </summary>
    /// <remarks>
    /// 集群唯一的作业写成存储里的一次性触发器，任一节点到期抢占；每个实例各自运行的作业在本实例进程内等待。
    /// </remarks>
    private async ValueTask ScheduleRetryAsync(JobRecord job, JobRun failed, JobRetryPolicy policy, DateTimeOffset failedAt)
    {
        var due = failedAt + policy.DelayFor(failed.Attempt);
        var retryOf = failed.RetryOf ?? failed.Id;
        if (job.RunOnEveryInstance)
        {
            lock (_lock)
            {
                _localRetries.Add(new PendingRetry(job.Name, (JsonObject)failed.Data.DeepClone(), failed.Attempt + 1, retryOf, due));
            }

            return;
        }

        var trigger = TransientTrigger(job.Name, RetryTriggerPrefix + JobRun.NewId(), JobTrigger.Retry, failed.Data, due);
        trigger.Attempt = failed.Attempt + 1;
        trigger.RetryOf = retryOf;
        var saved = await _store.SaveTriggersAsync([trigger], CancellationToken.None).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            LogStoreFailed(_logger, "retry-trigger", saved.Error!.Code);
        }
    }

    /// <summary>
    /// 运行本实例到期的重试。
    /// </summary>
    /// <remarks>
    /// 以存储里的当前作业为准：作业没了即丢弃，暂停了就等恢复，不允许并发的等本实例上的运行结束。
    /// </remarks>
    private async ValueTask FireLocalRetriesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<PendingRetry> due;
        lock (_lock)
        {
            due = [.. _localRetries.Where(retry => retry.DueAt <= now)];
            _localRetries.RemoveAll(retry => retry.DueAt <= now);
        }

        foreach (var retry in due)
        {
            var found = await _store.FindJobAsync(retry.Job, cancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var maybe))
            {
                LogStoreFailed(_logger, "retry-job", found.Error!.Code);
                Requeue(retry);
                continue;
            }

            if (!maybe.TryGetValue(out var job))
            {
                continue;
            }

            bool busy;
            lock (_lock)
            {
                busy = !job.AllowConcurrent && _running.Values.Any(running => running.Record.Job == job.Name);
            }

            if (job.Paused || busy)
            {
                Requeue(retry);
                continue;
            }

            if (!_types.Find(job.JobTypeName).TryGetValue(out var jobType))
            {
                LogTypeMissing(_logger, job.Name.Value, job.JobTypeName);
                continue;
            }

            var run = JobRun.Create(job.Name, retry.DueAt, JobTrigger.Retry, null, retry.Data) with { Attempt = retry.Attempt, RetryOf = retry.RetryOf };
            var record = NewRecord(run, now);
            var begun = await _store.TryBeginRunAsync(record, exclusive: false, cancellationToken).ConfigureAwait(false);
            if (!begun.TryGetValue(out _))
            {
                LogStoreFailed(_logger, "begin-run", begun.Error!.Code);
                Requeue(retry);
                continue;
            }

            StartBackground(record, run, jobType, job, null);
        }

        void Requeue(PendingRetry retry)
        {
            lock (_lock)
            {
                _localRetries.Add(retry);
            }
        }
    }

    /// <summary>
    /// 新增或替换一个作业及其触发器。
    /// </summary>
    /// <remarks>
    /// 按代码写入（<paramref name="source"/> 为 <see cref="JobSource.Code"/>）时记下代码定义的指纹；存储里是运行时改过、指纹没变的作业时保留运行时的修改，<paramref name="force"/> 为真时（恢复）照样按代码写入。
    /// 运行时替换代码作业时来源记为 <see cref="JobSource.Overridden"/> 并带上原来的指纹。
    /// </remarks>
    private ValueTask<Result> ScheduleOneAsync(JobDefinition definition, JobSource source, CancellationToken cancellationToken, bool force = false)
        => UnderTriggerLockAsync(() => ScheduleOneLockedAsync(definition, source, force, cancellationToken), cancellationToken);

    /// <summary>
    /// 在锁内新增或替换一个作业及其触发器。
    /// </summary>
    private async ValueTask<Result> ScheduleOneLockedAsync(JobDefinition definition, JobSource source, bool force, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!_types.KeyFor(definition.JobType).TryGetValue(out var typeKey))
        {
            return Error.NotFound(SchedulingErrors.JobTypeNotRegistered, SchedulingMessages.JobTypeNotRegistered, MessageArguments.Of(("type", definition.JobType.FullName ?? definition.JobType.Name)));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trigger in definition.Triggers)
        {
            if (!names.Add(trigger.Name))
            {
                return DefinitionInvalid(definition.Name, $"trigger '{trigger.Name}' is defined more than once");
            }

            if (trigger.Schedule.Calendar is { } calendar && !_calendars.Find(calendar).HasValue)
            {
                return DefinitionInvalid(definition.Name, $"calendar '{calendar}' is not registered");
            }
        }

        var existing = await _store.FindJobAsync(definition.Name, cancellationToken).ConfigureAwait(false);
        if (!existing.TryGetValue(out var existingMaybe))
        {
            return existing.Error!;
        }

        var previousTriggers = await _store.TriggersAsync(definition.Name, cancellationToken).ConfigureAwait(false);
        if (!previousTriggers.TryGetValue(out var previous))
        {
            return previousTriggers.Error!;
        }

        var now = _clock.GetUtcNow();
        var current = existingMaybe.TryGetValue(out var found) ? found : null;
        string? fingerprint = null;
        if (source == JobSource.Code)
        {
            fingerprint = FingerprintOf(definition, typeKey);
            if (current is { Source: JobSource.Overridden } overridden && !force)
            {
                if (string.Equals(overridden.CodeFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    // 代码定义没变：保留运行时的修改
                    return Result.Success();
                }

                LogOverrideDiscarded(_logger, definition.Name.Value);
            }
        }
        else if (current is { Source: JobSource.Code or JobSource.Overridden } coded)
        {
            // 运行时替换代码作业：记为改过，指纹沿用，启动时据此判断代码有没有变
            source = JobSource.Overridden;
            fingerprint = coded.CodeFingerprint;
        }

        var record = new JobRecord
        {
            Name = definition.Name,
            JobTypeName = typeKey,
            Data = definition.Data is null ? [] : (JsonObject)definition.Data.DeepClone(),
            AllowConcurrent = definition.AllowConcurrent,
            RunOnEveryInstance = definition.RunOnEveryInstance,
            RequestsRecovery = definition.RequestsRecovery,
            Description = definition.Description,
            Retry = definition.Retry,
            Timeout = definition.Timeout,
            Paused = current?.Paused ?? false,
            Source = source,
            UpdatedAt = now,
            CodeFingerprint = fingerprint,
        };
        var previousByName = previous.ToDictionary(static trigger => trigger.Name, StringComparer.Ordinal);
        var triggers = new List<TriggerRecord>(definition.Triggers.Count);
        foreach (var trigger in definition.Triggers)
        {
            var next = new TriggerRecord
            {
                Job = definition.Name,
                Name = trigger.Name,
                Schedule = trigger.Schedule,
                Data = trigger.Data is null ? [] : (JsonObject)trigger.Data.DeepClone(),
                Priority = trigger.Priority,
                MisfirePolicy = trigger.MisfirePolicy,
                Description = trigger.Description,
                Local = definition.RunOnEveryInstance,
                RunAs = JobTrigger.Schedule,
                State = record.Paused ? TriggerState.Paused : TriggerState.Waiting,
            };
            if (previousByName.TryGetValue(trigger.Name, out var prior) && string.Equals(prior.Schedule.ToString(), trigger.Schedule.ToString(), StringComparison.Ordinal))
            {
                // 计划没变：保留进度（上一次时刻、次数、等待中的下一次时刻与暂停状态）
                next.PreviousFireAt = prior.PreviousFireAt;
                next.FireCount = prior.FireCount;
                next.State = prior.State is TriggerState.Paused or TriggerState.Complete ? prior.State : next.State;
                next.NextFireAt = prior.State is TriggerState.Waiting or TriggerState.Paused or TriggerState.Blocked ? prior.NextFireAt : null;
            }

            if (next.State != TriggerState.Complete && next.NextFireAt is null)
            {
                var fire = NextFire(trigger.Schedule, now, next.FireCount);
                next.NextFireAt = fire;
                if (fire is null)
                {
                    next.State = TriggerState.Complete;
                }
            }

            triggers.Add(next);
        }

        var removed = previous.Where(prior => !prior.Transient && !names.Contains(prior.Name)).Select(static prior => prior.Key).ToList();
        var saved = await _store.SaveJobsAsync([record], cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return saved;
        }

        if (triggers.Count > 0)
        {
            saved = await _store.SaveTriggersAsync(triggers, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }
        }

        if (removed.Count > 0)
        {
            saved = await _store.DeleteTriggersAsync(removed, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }
        }

        lock (_lock)
        {
            _localDirty = true;
        }

        await PublishAsync(SchedulingTopics.JobScheduled, new SchedulingEventData { Job = definition.Name.Value, NodeId = NodeId, At = now }, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// 暂停或恢复作业（及其全部触发器）。
    /// </summary>
    private async ValueTask<IReadOnlyList<Result>> SetPausedAsync(IReadOnlyList<JobName> names, bool paused, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        await EnsureSyncedAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<Result>(names.Count);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await SetPausedOneAsync(name, paused, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// 暂停或恢复一个作业。
    /// </summary>
    private ValueTask<Result> SetPausedOneAsync(JobName name, bool paused, CancellationToken cancellationToken)
        => UnderTriggerLockAsync(() => SetPausedOneLockedAsync(name, paused, cancellationToken), cancellationToken);

    /// <summary>
    /// 在锁内暂停或恢复一个作业。
    /// </summary>
    private async ValueTask<Result> SetPausedOneLockedAsync(JobName name, bool paused, CancellationToken cancellationToken)
    {
        var found = await _store.FindJobAsync(name, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var job))
        {
            return JobNotFound(name);
        }

        var triggers = await _store.TriggersAsync(name, cancellationToken).ConfigureAwait(false);
        if (!triggers.TryGetValue(out var triggerList))
        {
            return triggers.Error!;
        }

        var now = _clock.GetUtcNow();
        job.Paused = paused;
        job.UpdatedAt = now;
        var changed = new List<TriggerRecord>();
        foreach (var trigger in triggerList)
        {
            if (ApplyPause(trigger, paused, now))
            {
                changed.Add(trigger);
            }
        }

        var saved = await _store.SaveJobsAsync([job], cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return saved;
        }

        if (changed.Count > 0)
        {
            saved = await _store.SaveTriggersAsync(changed, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }
        }

        lock (_lock)
        {
            _localDirty = true;
        }

        await PublishAsync(paused ? SchedulingTopics.JobPaused : SchedulingTopics.JobResumed, new SchedulingEventData { Job = name.Value, NodeId = NodeId, At = now }, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// 暂停或恢复单个触发器。
    /// </summary>
    private async ValueTask<IReadOnlyList<Result>> SetTriggersPausedAsync(IReadOnlyList<TriggerKey> keys, bool paused, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        await EnsureSyncedAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<Result>(keys.Count);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await UnderTriggerLockAsync(() => SetTriggerPausedLockedAsync(key, paused, cancellationToken), cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// 在锁内暂停或恢复一个触发器。
    /// </summary>
    private async ValueTask<Result> SetTriggerPausedLockedAsync(TriggerKey key, bool paused, CancellationToken cancellationToken)
    {
        var triggers = await _store.TriggersAsync(key.Job, cancellationToken).ConfigureAwait(false);
        if (!triggers.TryGetValue(out var triggerList))
        {
            return triggers.Error!;
        }

        var trigger = triggerList.FirstOrDefault(candidate => string.Equals(candidate.Name, key.Trigger, StringComparison.Ordinal));
        if (trigger is null)
        {
            return Error.NotFound(SchedulingErrors.TriggerNotFound, SchedulingMessages.TriggerNotFound, MessageArguments.Of(("job", key.Job.Value), ("trigger", key.Trigger)));
        }

        if (!ApplyPause(trigger, paused, _clock.GetUtcNow()))
        {
            return Result.Success();
        }

        var saved = await _store.SaveTriggersAsync([trigger], cancellationToken).ConfigureAwait(false);
        lock (_lock)
        {
            _localDirty = true;
        }

        return saved;
    }

    /// <summary>
    /// 在触发器写锁内执行。
    /// </summary>
    private async ValueTask<T> UnderTriggerLockAsync<T>(Func<ValueTask<T>> action, CancellationToken cancellationToken)
    {
        await _triggerWrites.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _triggerWrites.Release();
        }
    }

    /// <summary>
    /// 在触发器写锁内执行，没有返回值。
    /// </summary>
    private async ValueTask UnderTriggerLockAsync(Func<ValueTask> action, CancellationToken cancellationToken)
    {
        await _triggerWrites.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _triggerWrites.Release();
        }
    }

    /// <summary>
    /// 把触发器改成暂停或从暂停恢复。
    /// </summary>
    /// <remarks>
    /// 返回是否有改动。恢复时没有下一次时刻的（间隔计划在运行中被暂停）从现在推算。
    /// </remarks>
    private bool ApplyPause(TriggerRecord trigger, bool paused, DateTimeOffset now)
    {
        if (paused)
        {
            // 已抢占的也改成暂停：间隔计划整个运行期间保持已抢占，结束时的写回看到暂停只更新时刻、保持暂停
            if (trigger.State is TriggerState.Waiting or TriggerState.Blocked or TriggerState.Acquired)
            {
                trigger.State = TriggerState.Paused;
                trigger.AcquiredBy = null;
                trigger.AcquiredAt = null;
                return true;
            }

            return false;
        }

        if (trigger.State != TriggerState.Paused)
        {
            return false;
        }

        // Skip 策略下错过的到期时刻直接丢弃：恢复时就从现在推算，状态里不会出现一个不会运行的过去时刻；其他策略留到抢占时按 misfire 处理
        if (trigger.NextFireAt is null || (trigger.MisfirePolicy == MisfirePolicy.Skip && now - trigger.NextFireAt.Value > _options.MisfireThreshold))
        {
            trigger.NextFireAt = NextFire(trigger.Schedule, now, trigger.FireCount);
        }

        trigger.State = trigger.NextFireAt is null ? TriggerState.Complete : TriggerState.Waiting;
        return true;
    }

    /// <summary>
    /// 找作业记录：存储里没有时退到代码登记（尚未同步时也能手动触发与看状态）。
    /// </summary>
    private async ValueTask<Result<Maybe<JobRecord>>> FindJobRecordAsync(JobName name, CancellationToken cancellationToken)
    {
        var found = await _store.FindJobAsync(name, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (maybe.HasValue)
        {
            return maybe;
        }

        if (_codeJobs.Find(name).TryGetValue(out var definition) && _types.KeyFor(definition.JobType).TryGetValue(out var key))
        {
            return Maybe.Some(new JobRecord
            {
                Name = definition.Name,
                JobTypeName = key,
                Data = definition.Data is null ? [] : (JsonObject)definition.Data.DeepClone(),
                AllowConcurrent = definition.AllowConcurrent,
                RunOnEveryInstance = definition.RunOnEveryInstance,
                RequestsRecovery = definition.RequestsRecovery,
                Description = definition.Description,
                Retry = definition.Retry,
                Timeout = definition.Timeout,
                Source = JobSource.Code,
            });
        }

        return Maybe.None<JobRecord>();
    }

    /// <summary>
    /// 找作业记录与已登记的类型。
    /// </summary>
    private async ValueTask<Result<(JobRecord Record, Type Type)>> ResolveJobAsync(JobName name, CancellationToken cancellationToken)
    {
        var found = await FindJobRecordAsync(name, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var record))
        {
            return JobNotFound(name);
        }

        if (!_types.Find(record.JobTypeName).TryGetValue(out var type))
        {
            return Error.NotFound(SchedulingErrors.JobTypeNotRegistered, SchedulingMessages.JobTypeNotRegistered, MessageArguments.Of(("type", record.JobTypeName)));
        }

        return (record, type);
    }

    /// <summary>
    /// 组装状态：本地触发器的下一次时刻取本节点的推算。
    /// </summary>
    private JobStatus BuildStatus(JobRecord job, IReadOnlyList<TriggerRecord> triggers, int runningCount, JobRunRecord? last)
    {
        var statuses = new List<JobTriggerStatus>(triggers.Count);
        lock (_lock)
        {
            foreach (var trigger in triggers.OrderBy(static trigger => trigger.Name, StringComparer.Ordinal))
            {
                var status = trigger.ToStatus();
                if (trigger.Local && _local.TryGetValue(trigger.Key.ToString(), out var local))
                {
                    status = status with { NextFireAt = local.NextFireAt, PreviousFireAt = local.PreviousFireAt ?? status.PreviousFireAt, FireCount = local.FireCount };
                }

                statuses.Add(status);
            }
        }

        return new JobStatus(job, statuses, runningCount, last?.ToOutcome());
    }

    /// <summary>
    /// 到期触发器的 misfire 判定与计划时刻。
    /// </summary>
    /// <remarks>
    /// 返回推算下一次的基准时刻与本次运行的计划时刻；<paramref name="skip"/> 为真表示本次不运行。
    /// </remarks>
    private (DateTimeOffset NextFrom, DateTimeOffset ScheduledAt) Plan(TriggerRecord trigger, DateTimeOffset planned, DateTimeOffset now, out bool skip)
    {
        skip = false;
        if (now - planned <= _options.MisfireThreshold)
        {
            // 没有 misfire：下一次从计划时刻推算，节奏不漂移
            return (planned, planned);
        }

        switch (trigger.MisfirePolicy)
        {
            case MisfirePolicy.Skip:
                skip = true;
                return (now, now);
            case MisfirePolicy.RunAll:
                // 逐次补齐：本次按原计划时刻运行，下一次从该时刻推算，每个周期补一次直至追平
                return (planned, planned);
            default:
                return (now, now);
        }
    }

    /// <summary>
    /// 把触发器改回等待（或完成）。
    /// </summary>
    private static void ReleaseTrigger(TriggerRecord trigger, DateTimeOffset? next)
    {
        trigger.NextFireAt = next;
        trigger.State = next is null ? TriggerState.Complete : TriggerState.Waiting;
        trigger.AcquiredBy = null;
        trigger.AcquiredAt = null;
    }

    /// <summary>
    /// 套用日历与次数上限推算下一次。
    /// </summary>
    /// <remarks>
    /// 日历未登记按不排除处理（登记时已校验）。
    /// </remarks>
    private DateTimeOffset? NextFire(JobSchedule schedule, DateTimeOffset after, long fireCount)
    {
        IJobCalendar? calendar = null;
        if (schedule.Calendar is { } name && _calendars.Find(name).TryGetValue(out var found))
        {
            calendar = found;
        }

        return JobPlanner.NextFire(schedule, after, calendar, fireCount).Match(static next => (DateTimeOffset?)next, static () => null);
    }

    /// <summary>
    /// 一次性触发器：立即到期、运行一次后删除。
    /// </summary>
    private static TriggerRecord TransientTrigger(JobName job, string name, JobTrigger runAs, JsonObject? data, DateTimeOffset now) => new()
    {
        Job = job,
        Name = name,
        Schedule = JobSchedule.Once(now),
        Data = data is null ? [] : (JsonObject)data.DeepClone(),
        Priority = TransientPriority,
        MisfirePolicy = MisfirePolicy.RunOnce,
        State = TriggerState.Waiting,
        NextFireAt = now,
        Transient = true,
        RunAs = runAs,
    };

    /// <summary>
    /// 新的运行记录。
    /// </summary>
    private JobRunRecord NewRecord(JobRun run, DateTimeOffset startedAt) => new()
    {
        Id = run.Id,
        Job = run.Name,
        TriggerName = run.TriggerName,
        Trigger = run.Trigger,
        ScheduledAt = run.ScheduledAt,
        Attempt = run.Attempt,
        RetryOf = run.RetryOf,
        StartedAt = startedAt,
        State = JobRunState.Running,
        NodeId = NodeId,
        Data = (JsonObject)run.Data.DeepClone(),
    };

    /// <summary>
    /// 作业数据 ← 触发器数据 ← 手动数据逐层覆盖。
    /// </summary>
    private static JsonObject Merge(JsonObject? job, JsonObject? trigger, JsonObject? manual)
    {
        var merged = new JsonObject();
        foreach (var layer in new[] { job, trigger, manual })
        {
            if (layer is null)
            {
                continue;
            }

            foreach (var (key, value) in layer)
            {
                merged[key] = value?.DeepClone();
            }
        }

        return merged;
    }

    /// <summary>
    /// 写回一条触发器，失败只记日志。
    /// </summary>
    private async ValueTask SaveTriggerAsync(TriggerRecord trigger, CancellationToken cancellationToken)
    {
        var saved = await _store.SaveTriggersAsync([trigger], cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            LogStoreFailed(_logger, "save-trigger", saved.Error!.Code);
        }
    }

    /// <summary>
    /// 删除某作业的本地触发器状态。
    /// </summary>
    private void ForgetLocal(JobName name)
    {
        lock (_lock)
        {
            foreach (var key in _local.Keys.Where(key => key.StartsWith(name.Value + "/", StringComparison.Ordinal)).ToList())
            {
                _local.Remove(key);
            }
        }
    }

    /// <summary>
    /// 发布事件：装配了消息能力时经新作用域里的发布者发出。
    /// </summary>
    private async ValueTask PublishAsync(string topic, SchedulingEventData data, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var publisher = new SchedulingEventPublisher(scope.ServiceProvider.GetService<IMessagePublisher>(), _logger);
        if (publisher.IsEnabled)
        {
            await publisher.PublishAsync(topic, data, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 运行记录的事件数据。
    /// </summary>
    private SchedulingEventData EventOf(JobRunRecord record) => new()
    {
        Job = record.Job.Value,
        TriggerName = record.TriggerName,
        RunId = record.Id,
        Trigger = record.Trigger,
        State = record.State,
        ScheduledAt = record.ScheduledAt,
        StartedAt = record.StartedAt,
        CompletedAt = record.CompletedAt,
        ErrorCode = record.ErrorCode,
        ErrorMessage = record.ErrorMessage,
        Attempt = record.Attempt,
        RetryOf = record.RetryOf,
        NodeId = record.NodeId,
        At = _clock.GetUtcNow(),
    };

    /// <summary>
    /// 作业未登记。
    /// </summary>
    private static Error JobNotFound(JobName name) => Error.NotFound(SchedulingErrors.JobNotFound, SchedulingMessages.JobNotFound, MessageArguments.Of(("name", name.Value)));

    /// <summary>
    /// 定义不合法。
    /// </summary>
    private static Error DefinitionInvalid(JobName name, string reason) => Error.Validation(SchedulingErrors.DefinitionInvalid, SchedulingMessages.DefinitionInvalid, MessageArguments.Of(("name", name.Value), ("reason", reason)));

    /// <summary>
    /// 代码定义变了，运行时的修改被覆盖。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The code definition of job {Job} changed; the runtime changes made to it are replaced by the code definition.")]
    private static partial void LogOverrideDiscarded(ILogger logger, string job);

    /// <summary>
    /// 获取领导租约。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired the leader lease for '{Resource}'.")]
    private static partial void LogLeadershipAcquired(ILogger logger, string resource);

    /// <summary>
    /// 失去领导租约。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Lost the leader lease for '{Resource}'.")]
    private static partial void LogLeadershipLost(ILogger logger, string resource);

    /// <summary>
    /// 运行失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {Job} run {RunId} failed: {ErrorCode}")]
    private static partial void LogRunFailed(ILogger logger, string job, string runId, string errorCode);

    /// <summary>
    /// 运行抛出。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Job {Job} run {RunId} threw outside the pipeline.")]
    private static partial void LogRunThrew(ILogger logger, Exception exception, string job, string runId);

    /// <summary>
    /// 存储操作失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Job store operation '{Operation}' failed with '{ErrorCode}'.")]
    private static partial void LogStoreFailed(ILogger logger, string operation, string errorCode);

    /// <summary>
    /// 代码作业同步失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Writing code-defined job '{Job}' to the store failed with '{ErrorCode}'; retrying next cycle.")]
    private static partial void LogSyncFailed(ILogger logger, string job, string errorCode);

    /// <summary>
    /// 作业类型未登记。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Job '{Job}' references job type '{JobType}', which is not registered on this node.")]
    private static partial void LogTypeMissing(ILogger logger, string job, string jobType);

    /// <summary>
    /// 节点失联。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduler nodes [{Nodes}] missed their heartbeat and are being recovered.")]
    private static partial void LogNodesLost(ILogger logger, string nodes);

    /// <summary>
    /// 周期抛出。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "A scheduler cycle threw.")]
    private static partial void LogTickThrew(ILogger logger, Exception exception);

    /// <summary>
    /// 代码定义的指纹。
    /// </summary>
    /// <remarks>
    /// 类型键、数据、标志、说明、重试策略与执行时限（设置了才计入，ADR-0060）与各触发器（名字、计划文本、数据、优先级、misfire 策略、说明）按行拼接后的 SHA-256 十六进制。
    /// </remarks>
    private static string FingerprintOf(JobDefinition definition, string typeKey)
    {
        var text = new StringBuilder()
            .Append(typeKey).Append('\n')
            .Append(definition.Data?.ToJsonString() ?? "{}").Append('\n')
            .Append(definition.AllowConcurrent).Append(definition.RunOnEveryInstance).Append(definition.RequestsRecovery).Append('\n')
            .Append(definition.Description).Append('\n');
        if (definition.Retry is { } retry)
        {
            text.Append("retry ").Append(retry.MaxRetries.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(retry.Delay.Ticks.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(retry.Backoff).Append(' ')
                .Append(retry.MaxDelay?.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        if (definition.Timeout is { } timeout)
        {
            text.Append("timeout ").Append(timeout.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        foreach (var trigger in definition.Triggers)
        {
            text.Append(trigger.Name).Append('\t')
                .Append(trigger.Schedule.ToString()).Append('\t')
                .Append(trigger.Data?.ToJsonString() ?? "{}").Append('\t')
                .Append(trigger.Priority.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(trigger.MisfirePolicy).Append('\t')
                .Append(trigger.Description).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
