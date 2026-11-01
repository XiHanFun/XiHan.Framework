// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 一个 S3 存储实例的条件写入状态：探明服务认条件写入之前，不放行不覆盖写入。
/// </summary>
/// <remarks>
/// 状态只往前走：尚未确认 → 已确认，或尚未确认 → 不认；关闭条件写入时一直是关闭（ADR-0089、ADR-0090）。启动或建实例时经 <see cref="StartAsync"/> 探一次：探明不认即返回失败（调用方启动失败或不建实例）；连不上、被拒绝、探测桶不存在都是尚未确认，照常启动，在后台按退避重试（第一次 <see cref="InitialRetryDelay"/> 后，每次加倍，最长 <see cref="S3ObjectStorageOptions.HealthProbeInterval"/>，按注入的时钟计时），有了终态即停止。不覆盖写入经 <see cref="RefuseAsync"/> 放行或拒绝：尚未确认时等正在进行的探测，或距上一次探测满最小间隔时就地探一次（每个实例同一时刻只有一个探测，后台重试与写入共用），仍未确认返回 <see cref="ObjectStorageErrors.ConditionalWriteUnconfirmed"/>，探明不认返回 <see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>。健康项经 <see cref="Annotate"/> 与 <see cref="Health"/> 反映当前状态。
/// </remarks>
internal sealed partial class S3ConditionalWriteGate : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 第一次后台重试的延迟，也是写入时就地探测的最小间隔。
    /// </summary>
    /// <remarks>
    /// <see cref="S3ObjectStorageOptions.HealthProbeInterval"/> 比它短时两者都取那个间隔。
    /// </remarks>
    internal static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 客户端。
    /// </summary>
    private readonly IAmazonS3 _client;

    /// <summary>
    /// 选项（探测桶、建桶、单次探测的超时与重试上限）。
    /// </summary>
    private readonly S3ObjectStorageOptions _options;

    /// <summary>
    /// 是否也探测分片上传的条件完成。
    /// </summary>
    private readonly bool _multipart;

    /// <summary>
    /// 时钟：重试计时、单次探测的超时与就地探测的间隔都按它。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 存储名（日志用）：缺省实例为 <c>default</c>，命名实例为实例名。
    /// </summary>
    private readonly string _store;

    /// <summary>
    /// 第一次重试的延迟与就地探测的最小间隔。
    /// </summary>
    private readonly TimeSpan _firstDelay;

    /// <summary>
    /// 保护下列可变状态。
    /// </summary>
    private readonly Lock _sync = new();

    /// <summary>
    /// 停止信号：结束后台重试并取消正在进行的探测。
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 有了终态（已确认或不认）时完成。
    /// </summary>
    private readonly TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 等待后台重试排定到第几次的测试钩子。
    /// </summary>
    private readonly List<(int Count, TaskCompletionSource Signal)> _scheduledWaiters = [];

    /// <summary>
    /// 后台重试已排定的延迟，按次序（测试核对退避用）。
    /// </summary>
    private readonly List<TimeSpan> _scheduledDelays = [];

    /// <summary>
    /// 当前状态。
    /// </summary>
    private GateState _state;

    /// <summary>
    /// 最近一次探测失败的错误。
    /// </summary>
    /// <remarks>
    /// 不认时是不认的错误；已确认与关闭时为 <see langword="null"/>。
    /// </remarks>
    private Error? _error;

    /// <summary>
    /// 最近一次探测结束的时刻。
    /// </summary>
    /// <remarks>
    /// 还没探过为 <see langword="null"/>。
    /// </remarks>
    private DateTimeOffset? _lastAttemptEnded;

    /// <summary>
    /// 正在进行的探测。
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>。
    /// </remarks>
    private Task<Result>? _inflight;

    /// <summary>
    /// 后台重试。
    /// </summary>
    /// <remarks>
    /// 还没起为 <see langword="null"/>。
    /// </remarks>
    private Task? _retrying;

    /// <summary>
    /// 是否已停止（停止后不再起新的探测）。
    /// </summary>
    private bool _stopped;

    /// <summary>
    /// 是否已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 创建。
    /// </summary>
    private S3ConditionalWriteGate(IAmazonS3 client, S3ObjectStorageOptions options, bool multipart, TimeProvider clock, ILogger logger, string store, GateState state)
    {
        _client = client;
        _options = options;
        _multipart = multipart;
        _clock = clock;
        _logger = logger;
        _store = store;
        _state = state;
        _firstDelay = InitialRetryDelay < options.HealthProbeInterval ? InitialRetryDelay : options.HealthProbeInterval;
        if (state is not GateState.Unconfirmed)
        {
            _settled.SetResult();
        }
    }

    /// <summary>
    /// 条件写入的状态。
    /// </summary>
    private enum GateState
    {
        /// <summary>
        /// 关闭（<c>ConditionalWrites = false</c>）：不探测，不覆盖写入一律拒绝。
        /// </summary>
        Disabled,

        /// <summary>
        /// 尚未确认：还没探过，或探测连不上、被拒绝、探测桶不存在。
        /// </summary>
        Unconfirmed,

        /// <summary>
        /// 已确认服务认条件写入（或经公开构造函数建的存储，由调用方负责）。
        /// </summary>
        Confirmed,

        /// <summary>
        /// 探明服务不认条件写入。
        /// </summary>
        Unsupported,
    }

    /// <summary>
    /// 有了终态（已确认或不认）时完成。
    /// </summary>
    /// <remarks>
    /// 关闭与不探测的状态一开始就完成。
    /// </remarks>
    internal Task Settled => _settled.Task;

    /// <summary>
    /// 后台重试已排定的延迟，按次序。
    /// </summary>
    internal IReadOnlyList<TimeSpan> ScheduledDelays
    {
        get
        {
            lock (_sync)
            {
                return [.. _scheduledDelays];
            }
        }
    }

    /// <summary>
    /// 建一个要探测的状态。
    /// </summary>
    /// <remarks>
    /// <see cref="S3ObjectStorageOptions.ConditionalWrites"/> 关闭时为关闭状态，不探测；开启时 <see cref="S3ObjectStorageOptions.HealthBucket"/> 必须有值（<see cref="S3ConditionalWriteProbe.RequireBucket"/> 已核对）。
    /// </remarks>
    /// <param name="client">客户端。</param>
    /// <param name="options">选项。</param>
    /// <param name="multipart">是否也探测分片上传的条件完成。</param>
    /// <param name="clock">时钟。</param>
    /// <param name="logger">日志。</param>
    /// <param name="store">存储名（日志用）。</param>
    internal static S3ConditionalWriteGate Create(IAmazonS3 client, S3ObjectStorageOptions options, bool multipart, TimeProvider clock, ILogger logger, string store)
        => new(client, options, multipart, clock, logger, store, options.ConditionalWrites ? GateState.Unconfirmed : GateState.Disabled);

    /// <summary>
    /// 不探测的状态：经公开构造函数直接建的存储，服务是否认条件写入由调用方负责。
    /// </summary>
    /// <remarks>
    /// 条件写入开启时视作已确认，关闭时为关闭状态。
    /// </remarks>
    internal static S3ConditionalWriteGate Unprobed(IAmazonS3 client, S3ObjectStorageOptions options, TimeProvider clock)
        => new(client, options, multipart: false, clock, NullLogger.Instance, "unprobed", options.ConditionalWrites ? GateState.Confirmed : GateState.Disabled);

    /// <summary>
    /// 启动或建实例时探一次，尚未确认就起后台重试。
    /// </summary>
    /// <remarks>
    /// 只有探明服务不认条件写入时返回失败（<see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>），调用方据此启动失败或不建实例；连不上、被拒绝、探测桶不存在都返回成功，状态是尚未确认。调用方的取消原样抛出。重复调用不再起第二个后台重试。
    /// </remarks>
    internal async ValueTask<Result> StartAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_state is not GateState.Unconfirmed)
            {
                return Result.Success();
            }
        }

        await ProbeShared().WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            if (_state is GateState.Unsupported && _error is { } unsupported)
            {
                return Result.Failure(unsupported);
            }

            // 尚未确认（含被拒绝与缺桶）：不阻止启动，在后台按退避重试；停止后不再起
            if (_state is GateState.Unconfirmed && _retrying is null && !_stopped)
            {
                var stopping = _stopping.Token;
                _retrying = Task.Run(() => RetryAsync(stopping), CancellationToken.None);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 不覆盖写入之前调用：放行返回 <see langword="null"/>，否则返回拒绝的原因。
    /// </summary>
    /// <remarks>
    /// 已确认放行；关闭返回 <see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>（消息键 <see cref="ObjectStorageMessages.ConditionalWriteDisabled"/>）；不认返回探明时的 <see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>。尚未确认时有探测在进行就等它；没有而距上一次探测结束已满最小间隔就地探一次；否则不探测。之后仍未确认返回 <see cref="ObjectStorageErrors.ConditionalWriteUnconfirmed"/>（参数 <c>reason</c> 是最近一次探测失败的错误码）。调用方的取消原样抛出；共用的探测不因某个写入方取消而中止。
    /// </remarks>
    internal async ValueTask<Error?> RefuseAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        Task<Result>? probe = null;
        lock (_sync)
        {
            if (_state is not GateState.Unconfirmed)
            {
                return Refusal(container, key);
            }

            // 尚未确认：已有探测就等它；没有就看间隔——写入密集时不能把探测压成连发，最小间隔内直接拒绝
            if (_inflight is not null)
            {
                probe = _inflight;
            }
            else if (!_stopped && (_lastAttemptEnded is not { } ended || _clock.GetUtcNow() - ended >= _firstDelay))
            {
                probe = StartProbeLocked();
            }
        }

        if (probe is not null)
        {
            await probe.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_sync)
        {
            return Refusal(container, key);
        }
    }

    /// <summary>
    /// 把条件写入的状态并进连通性的健康条目。
    /// </summary>
    /// <remarks>
    /// 数据 <c>conditionalWrites</c> 为 <c>confirmed</c> / <c>unconfirmed</c> / <c>unsupported</c> / <c>disabled</c>。尚未确认时 Degraded，错误是最近一次探测失败（连通性探测成功也一样）；不认时 Failed，错误是不认的错误；其余照连通性的状态。
    /// </remarks>
    internal ContributorStatus Annotate(ContributorStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        GateState state;
        Error? error;
        lock (_sync)
        {
            state = _state;
            error = _error;
        }

        var data = new Dictionary<string, object?>(status.Data, StringComparer.Ordinal) { ["conditionalWrites"] = Describe(state) };
        return state switch
        {
            // 不认是终态，要换服务或关闭条件写入后重启：与"暂时降级"区分开
            GateState.Unsupported => status with { State = LifecycleState.Failed, Error = error, Data = data },
            GateState.Unconfirmed => status with { State = LifecycleState.Degraded, Error = error ?? status.Error, Data = data },
            _ => status with { Data = data },
        };
    }

    /// <summary>
    /// 条件写入的状态作为探测结果（命名实例的健康项用）。
    /// </summary>
    /// <remarks>
    /// 已确认与关闭为成功；尚未确认与不认为失败，错误同 <see cref="Annotate"/>。
    /// </remarks>
    internal Result Health()
    {
        lock (_sync)
        {
            return _state is GateState.Unconfirmed or GateState.Unsupported && _error is { } error ? Result.Failure(error) : Result.Success();
        }
    }

    /// <summary>
    /// 后台重试排定到第 <paramref name="count"/> 次时完成。
    /// </summary>
    /// <remarks>
    /// 测试钩子：推进假时钟之前先等计时器排定，否则推进可能落在排定之前、被错过。
    /// </remarks>
    internal Task WhenRetryScheduledAsync(int count)
    {
        lock (_sync)
        {
            if (_scheduledDelays.Count >= count)
            {
                return Task.CompletedTask;
            }

            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _scheduledWaiters.Add((count, signal));
            return signal.Task;
        }
    }

    /// <summary>
    /// 停止后台重试，取消并等待正在进行的探测。
    /// </summary>
    /// <remarks>
    /// 状态保留；停止后写入不再触发探测。
    /// </remarks>
    internal async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? retrying;
        Task? inflight;
        bool first;
        lock (_sync)
        {
            first = !_stopped;
            _stopped = true;
            retrying = _retrying;
            inflight = _inflight;
        }

        // 停止信号只发一次：释放之后再调用不能碰已释放的令牌源
        if (first)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            // 后台重试与探测都不抛出（取消已转成结果），这里只等它们放下客户端
            await Task.WhenAll(retrying ?? Task.CompletedTask, inflight ?? Task.CompletedTask).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 调用方等不及：停止信号已发出，它们会自行结束
        }
    }

    /// <summary>
    /// 同步释放：只发出停止信号，不等待（容器同步释放时走这里）。
    /// </summary>
    /// <remarks>
    /// 令牌源不在这里释放：后台重试与探测可能仍在观察它，它们见到取消后自行结束；令牌源没有挂计时器，不释放也不泄漏。
    /// </remarks>
    public void Dispose() => Cancel();

    /// <summary>
    /// 只发出停止信号，不等待（同步释放用）。
    /// </summary>
    internal void Cancel()
    {
        bool first;
        lock (_sync)
        {
            first = !_stopped;
            _stopped = true;
        }

        if (first)
        {
            _stopping.Cancel();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _stopping.Dispose();
    }

    /// <summary>
    /// 状态名。
    /// </summary>
    private static string Describe(GateState state) => state switch
    {
        GateState.Confirmed => "confirmed",
        GateState.Unsupported => "unsupported",
        GateState.Disabled => "disabled",
        _ => "unconfirmed",
    };

    /// <summary>
    /// 按当前状态拒绝或放行（在锁内调用）。
    /// </summary>
    private Error? Refusal(ContainerName container, BlobKey key) => _state switch
    {
        GateState.Confirmed => null,
        GateState.Disabled => Error.Unavailable(ObjectStorageErrors.ConditionalWriteUnavailable, ObjectStorageMessages.ConditionalWriteDisabled, MessageArguments.Of(("container", container.Value), ("key", key.Value))),
        GateState.Unsupported => _error,
        _ => Error.Unavailable(ObjectStorageErrors.ConditionalWriteUnconfirmed, ObjectStorageMessages.ConditionalWriteUnconfirmed, MessageArguments.Of(("container", container.Value), ("key", key.Value), ("reason", _error?.Code ?? "not probed yet"))),
    };

    /// <summary>
    /// 共用的探测：有正在进行的就返回它，没有就起一个。
    /// </summary>
    private Task<Result> ProbeShared()
    {
        lock (_sync)
        {
            return _inflight ?? StartProbeLocked();
        }
    }

    /// <summary>
    /// 起一个探测（在锁内调用）。
    /// </summary>
    /// <remarks>
    /// 在线程池上执行：探测结束时要回到锁里记结论，不能在持锁的线程上同步跑完。
    /// </remarks>
    private Task<Result> StartProbeLocked()
    {
        var probe = Task.Run(ProbeAndRecordAsync, CancellationToken.None);
        _inflight = probe;
        return probe;
    }

    /// <summary>
    /// 探一次并记下结论，不抛出。
    /// </summary>
    private async Task<Result> ProbeAndRecordAsync()
    {
        Result outcome;
        try
        {
            outcome = await S3ConditionalWriteProbe.RunAsync(_client, _options, _multipart, _clock, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception) when (_stopping.IsCancellationRequested)
        {
            // 停止或释放中（客户端可能已释放）：这次的结论没有意义，不改状态
            lock (_sync)
            {
                _inflight = null;
            }

            return Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", "The store is stopping.")));
        }
        catch (Exception exception)
        {
            // 探测没有归类的异常（客户端的意外错误）：按连不上记为尚未确认，健康项与日志都报告它，后台照常重试
            LogProbeCrashed(_logger, exception, _store, _options.HealthBucket);
            outcome = Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", exception.Message)));
        }

        Record(outcome);
        return outcome;
    }

    /// <summary>
    /// 记下一次探测的结论。
    /// </summary>
    /// <remarks>
    /// 只在尚未确认时改变状态：通过 → 已确认，不认 → 不认，其余仍是尚未确认、记下错误；状态或错误码变化时记日志。
    /// </remarks>
    private void Record(Result outcome)
    {
        GateState previous;
        GateState current;
        string? previousCode;
        Error? error;
        lock (_sync)
        {
            previous = _state;
            previousCode = _error?.Code;
            _inflight = null;
            _lastAttemptEnded = _clock.GetUtcNow();
            if (_state is GateState.Unconfirmed)
            {
                // 状态机：已确认与不认是终态（服务的能力不会在运行中改变），只从尚未确认迁出
                if (outcome.Error is not { } failure)
                {
                    _state = GateState.Confirmed;
                    _error = null;
                }
                else if (failure.Code == ObjectStorageErrors.ConditionalWriteUnavailable)
                {
                    _state = GateState.Unsupported;
                    _error = failure;
                }
                else
                {
                    _error = failure;
                }
            }

            current = _state;
            error = _error;
        }

        if (current != previous)
        {
            _settled.TrySetResult();
        }

        switch (current)
        {
            case GateState.Confirmed when previous is GateState.Unconfirmed && previousCode is not null:
                LogConfirmed(_logger, _store, _options.HealthBucket);
                break;
            case GateState.Unsupported when previous is GateState.Unconfirmed && error is not null:
                LogUnsupported(_logger, _store, error.Code, error.Message);
                break;
            case GateState.Unconfirmed when error is not null && !string.Equals(previousCode, error.Code, StringComparison.Ordinal):
                // 只在原因变化时记：稳定的失败（一直连不上）不随每次重试刷屏
                LogUnconfirmed(_logger, _store, error.Code, error.Message);
                break;
        }
    }

    /// <summary>
    /// 后台重试：按退避等待，再共用探测，直到有了终态或停止。
    /// </summary>
    private async Task RetryAsync(CancellationToken stopping)
    {
        var delay = _firstDelay;
        while (true)
        {
            // 先排定计时器再报告排定：测试推进假时钟时计时器已经在等，不会错过
            var wait = Task.Delay(delay, _clock, stopping);
            MarkScheduled(delay);
            try
            {
                await wait.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Task<Result> probe;
            lock (_sync)
            {
                // 期间写入的就地探测可能已经有了终态；已停止则不再探
                if (_state is not GateState.Unconfirmed || _stopped)
                {
                    return;
                }

                probe = _inflight ?? StartProbeLocked();
            }

            await probe.ConfigureAwait(false);
            lock (_sync)
            {
                if (_state is not GateState.Unconfirmed || _stopped)
                {
                    return;
                }
            }

            // 退避：每次加倍，上限是健康探测间隔——长时间连不上时与健康项同一节奏，有界
            var doubled = delay * 2;
            delay = doubled < _options.HealthProbeInterval ? doubled : _options.HealthProbeInterval;
        }
    }

    /// <summary>
    /// 记下又排定了一次后台重试，唤醒等到这一次的测试钩子。
    /// </summary>
    private void MarkScheduled(TimeSpan delay)
    {
        List<TaskCompletionSource> ready = [];
        lock (_sync)
        {
            _scheduledDelays.Add(delay);
            for (var index = _scheduledWaiters.Count - 1; index >= 0; index--)
            {
                if (_scheduledWaiters[index].Count <= _scheduledDelays.Count)
                {
                    ready.Add(_scheduledWaiters[index].Signal);
                    _scheduledWaiters.RemoveAt(index);
                }
            }
        }

        foreach (var signal in ready)
        {
            signal.TrySetResult();
        }
    }

    /// <summary>
    /// 条件写入尚未确认（或原因变了）。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Conditional writes of S3 store '{Store}' are not confirmed yet ({Code}: {Message}); writes without overwrite are refused until a probe succeeds, retrying in the background.")]
    private static partial void LogUnconfirmed(ILogger logger, string store, string code, string message);

    /// <summary>
    /// 重试后确认了条件写入。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Conditional writes of S3 store '{Store}' are confirmed in bucket '{Bucket}'; writes without overwrite are accepted.")]
    private static partial void LogConfirmed(ILogger logger, string store, string? bucket);

    /// <summary>
    /// 启动之后才探明服务不认条件写入。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "The service behind S3 store '{Store}' ignores conditional writes ({Code}: {Message}); writes without overwrite are refused. Use a service that honours 'If-None-Match: *', or set 'ConditionalWrites' to 'false' and restart.")]
    private static partial void LogUnsupported(ILogger logger, string store, string code, string message);

    /// <summary>
    /// 探测抛出了没有归类的异常。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "The conditional write probe of S3 store '{Store}' in bucket '{Bucket}' failed unexpectedly.")]
    private static partial void LogProbeCrashed(ILogger logger, Exception exception, string store, string? bucket);
}
