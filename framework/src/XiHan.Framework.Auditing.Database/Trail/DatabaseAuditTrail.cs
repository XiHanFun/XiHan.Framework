// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Auditing.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Database.Trail;

/// <summary>
/// 数据库审计（单例）
/// </summary>
/// <remarks>
/// 写入只是进有界队列，不阻塞调用路径，满了丢弃并计数；后台循环攒够一批或到刷写间隔就把队列里的事件一次批量落库，失败只记日志、丢弃该批并计数，不重试；查询经仓储按写入序号升序分页，续页令牌是上一页最后一条的序号。读写都在根提供者的新作用域里各开一个工作单元（<see cref="UnitOfWorkScope"/>，ADR-0259），不加入调用方的事务
/// <para>
/// 不经 <c>App.InvokeAsync</c> 再走一遍管道：写入由 Observe 阶段的中间件或后台线程发起、查询可能来自 Authorize 阶段之前，再过 Tenant / Authorize 阶段会在要求租户时失败，而且启动与停止期间应用不在服务状态也要能落库。审计记录是平台级数据：实体不实现 <c>IMultiTenant</c>，工作单元不带租户
/// </para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DatabaseAuditTrail : IAuditSink, IAuditTrail, IDisposable
{
    /// <summary>
    /// 丢弃警告的节流窗口：窗口内只记一条，附上累计丢弃数
    /// </summary>
    private static readonly TimeSpan _dropWarningInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 查询用的只读、无租户的工作单元选项
    /// </summary>
    private static readonly UnitOfWorkOptions _readOnly = new() { IsReadOnly = true };

    /// <summary>
    /// 选项
    /// </summary>
    private readonly DatabaseAuditingOptions _options;

    /// <summary>
    /// 记录 ID 生成器
    /// </summary>
    private readonly IIdGenerator<Guid> _ids;

    /// <summary>
    /// 时钟：写入序号、刷写定时与警告节流都按它
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 根服务提供者
    /// </summary>
    /// <remarks>
    /// 每次读写开新作用域
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 等待落库的事件
    /// </summary>
    /// <remarks>
    /// 有界，满了丢弃新到的
    /// </remarks>
    private readonly Channel<AuditEvent> _queue;

    /// <summary>
    /// 批满信号
    /// </summary>
    /// <remarks>
    /// 队列攒够一批时唤醒后台循环，不等刷写间隔；容量 1，重复信号丢弃
    /// </remarks>
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <summary>
    /// 刷写互斥：后台循环与 <see cref="FlushAsync"/> 不同时出队
    /// </summary>
    private readonly SemaphoreSlim _flushing = new(1, 1);

    /// <summary>
    /// 上一个分配的写入序号
    /// </summary>
    /// <remarks>
    /// 只在持有 <see cref="_flushing"/> 时读写
    /// </remarks>
    private long _lastSequence;

    /// <summary>
    /// 因队列满或已停止而丢弃的事件数
    /// </summary>
    private long _dropped;

    /// <summary>
    /// 落库失败的事件数
    /// </summary>
    private long _failed;

    /// <summary>
    /// 已落库的事件数
    /// </summary>
    private long _persisted;

    /// <summary>
    /// 上一条丢弃警告的时钟刻度
    /// </summary>
    private long _lastDropWarningTicks;

    /// <summary>
    /// 创建
    /// </summary>
    public DatabaseAuditTrail(IServiceProvider services, DatabaseAuditingOptions options, IIdGenerator<Guid> ids, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DatabaseAuditTrail>();
        _services = services ?? throw new ArgumentNullException(nameof(services));
        // Wait 模式下 TryWrite 不阻塞、满了返回 false，丢弃才能被计数；DropWrite 模式的 TryWrite 总返回 true，丢了也不知道
        _queue = Channel.CreateBounded<AuditEvent>(new BoundedChannelOptions(options.QueueCapacity) { FullMode = BoundedChannelFullMode.Wait });
    }

    /// <summary>
    /// 因队列满或已停止而丢弃的事件数
    /// </summary>
    public long DroppedCount => Interlocked.Read(ref _dropped);

    /// <summary>
    /// 落库失败（整批异常或逐项被拒）的事件数
    /// </summary>
    public long FailedCount => Interlocked.Read(ref _failed);

    /// <summary>
    /// 已落库的事件数
    /// </summary>
    public long PersistedCount => Interlocked.Read(ref _persisted);

    /// <summary>
    /// 队列里等待落库的事件数
    /// </summary>
    public int PendingCount => _queue.Reader.Count;

    /// <inheritdoc />
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (_options.LogEvents)
        {
            LogAudit(_logger, auditEvent.Action, auditEvent.Outcome.IsSuccess ? "success" : auditEvent.Outcome.Error!.Code, auditEvent.PrincipalId, auditEvent.TenantId, auditEvent.Resource, auditEvent.TraceId);
        }

        // 有界且不阻塞：满了（或已停止）直接丢弃并计数，警告按窗口节流；攒够一批就唤醒后台循环
        if (!_queue.Writer.TryWrite(auditEvent))
        {
            var dropped = Interlocked.Increment(ref _dropped);
            WarnDropped(auditEvent.Action, dropped);
        }
        else if (_queue.Reader.Count >= _options.BatchSize)
        {
            _wake.Writer.TryWrite(true);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<Result<AuditPage>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var validation = query.Validate(AuditQuery.Revision);
        if (validation.IsFailure)
        {
            return validation.Error!;
        }

        long after = 0;
        if (query.ContinuationToken is { } token && (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out after) || after < 0))
        {
            return Error.Validation(AuditingErrors.QueryInvalid, AuditingMessages.QueryInvalid, MessageArguments.Of(("reason", "continuation token is not recognized")));
        }

        // 多取一条判断是否有下一页；续页令牌是本页最后一条的序号。页大小已到查询的取出上限时多不出这一条，
        // 改为满页之后再从最后一条之后取一条看还有没有
        var probe = query.PageSize < EntityQuery.DefaultMaxTake;
        var entityQuery = Filter(query, after).OrderBy(static r => r.Sequence).Page(0, probe ? query.PageSize + 1 : query.PageSize);
        var read = await UnitOfWorkScope.RunAsync<IReadOnlyList<AuditRecord>>(
            _services,
            _readOnly,
            async (services, ct) =>
            {
                var repository = services.GetRequiredService<IRepository<AuditRecord, Guid>>();
                var page = await repository.ListAsync(entityQuery, ct).ConfigureAwait(false);
                if (probe || page.Count < query.PageSize)
                {
                    return Result.Success(page);
                }

                var next = await repository.ListAsync(Filter(query, page[^1].Sequence).OrderBy(static r => r.Sequence).Page(0, 1), ct).ConfigureAwait(false);
                IReadOnlyList<AuditRecord> extended = next.Count == 0 ? page : [.. page, next[0]];
                return Result.Success(extended);
            },
            QueryFailed,
            _logger,
            cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var rows))
        {
            // 抛出的异常已经映射成存储不可用；剩下的只有提交返回的失败，以它的错误码为原因、原错误为内部错误
            if (string.Equals(read.Error!.Code, AuditingErrors.TrailUnavailable, StringComparison.Ordinal))
            {
                return read.Error;
            }

            LogQueryCommitFailed(_logger, read.Error.Code);
            return new Error(AuditingErrors.TrailUnavailable, AuditingMessages.TrailUnavailable, MessageArguments.Of(("reason", read.Error.Code)), read.Error);
        }

        var more = rows.Count > query.PageSize;
        var count = more ? query.PageSize : rows.Count;
        var events = new AuditEvent[count];
        for (var index = 0; index < count; index++)
        {
            events[index] = AuditRecordMapping.ToEvent(rows[index]);
        }

        return new AuditPage { Items = events, ContinuationToken = more ? rows[count - 1].Sequence.ToString(CultureInfo.InvariantCulture) : null };
    }

    /// <summary>
    /// 立即把队列里的事件全部落库并等待完成
    /// </summary>
    /// <remarks>
    /// 与后台循环互斥，循环正在刷写时等它这一批结束再接着刷剩下的
    /// </remarks>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        // 刷写串行：序号按出队顺序分配并按同一顺序提交，续页才不会越过尚未落库的事件
        await _flushing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var batch = TakeBatch(); batch.Count > 0; batch = TakeBatch())
            {
                await PersistAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _flushing.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _flushing.Dispose();

    /// <summary>
    /// 后台循环
    /// </summary>
    /// <remarks>
    /// 到刷写间隔或收到批满信号就刷写，直到取消；取消后剩余的事件由停止流程另行刷写
    /// </remarks>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.FlushInterval, _clock);
        Task<bool>? tick = null;
        Task<bool>? wake = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // 两种唤醒各保留一个在途等待：PeriodicTimer 不允许并发等待；批满信号也只在上一个等待完成后才重新发起，不会堆积
                tick ??= timer.WaitForNextTickAsync(cancellationToken).AsTask();
                wake ??= _wake.Reader.ReadAsync(cancellationToken).AsTask();
                var completed = await Task.WhenAny(tick, wake).ConfigureAwait(false);
                if (completed == tick)
                {
                    tick = null;
                }
                else
                {
                    wake = null;
                }

                if (completed.IsCanceled)
                {
                    return;
                }

                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 停止
        }
    }

    /// <summary>
    /// 停止接收
    /// </summary>
    /// <remarks>
    /// 之后写入的事件按丢弃计数；队列里已有的仍可被刷写
    /// </remarks>
    internal void Complete() => _queue.Writer.TryComplete();

    /// <summary>
    /// 按查询的过滤项组合规约
    /// </summary>
    /// <remarks>
    /// 可空字符串列只写相等比较，空值在 SQL 里本就不相等
    /// </remarks>
    private static EntityQuery<AuditRecord> Filter(AuditQuery query, long after)
    {
        var filtered = EntityQuery.All<AuditRecord>();
        if (after > 0)
        {
            filtered = filtered.Where(new Specification<AuditRecord>(r => r.Sequence > after));
        }

        if (query.From is { } from)
        {
            filtered = filtered.Where(new Specification<AuditRecord>(r => r.Timestamp >= from));
        }

        if (query.To is { } to)
        {
            filtered = filtered.Where(new Specification<AuditRecord>(r => r.Timestamp <= to));
        }

        if (query.Action is { } action)
        {
            filtered = filtered.Where(new Specification<AuditRecord>(r => r.Action == action));
        }

        // 「只要匿名 / 全局」写成字面的 null 比较：与捕获的 null 变量比较，有的 Provider 会生成 = NULL 而不是 IS NULL
        if (!query.Principal.IsAny)
        {
            filtered = query.Principal.PrincipalId is { } principal
                ? filtered.Where(new Specification<AuditRecord>(r => r.PrincipalId == principal))
                : filtered.Where(new Specification<AuditRecord>(r => r.PrincipalId == null));
        }

        if (!query.Tenant.IsAny)
        {
            filtered = query.Tenant.TenantId is { } tenant
                ? filtered.Where(new Specification<AuditRecord>(r => r.TenantId == tenant))
                : filtered.Where(new Specification<AuditRecord>(r => r.TenantId == null));
        }

        if (query.TraceId is { } trace)
        {
            filtered = filtered.Where(new Specification<AuditRecord>(r => r.TraceId == trace));
        }

        if (query.Resource is { } resource)
        {
            filtered = filtered.Where(new Specification<AuditRecord>(r => r.Resource == resource));
        }

        if (query.Succeeded is { } succeeded)
        {
            filtered = filtered.Where(new Specification<AuditRecord>(r => r.Succeeded == succeeded));
        }

        return filtered;
    }

    /// <summary>
    /// 从队列取出最多一批的事件
    /// </summary>
    /// <remarks>
    /// 队列为空返回空列表
    /// </remarks>
    private List<AuditEvent> TakeBatch()
    {
        var batch = new List<AuditEvent>(Math.Min(_options.BatchSize, Math.Max(_queue.Reader.Count, 1)));
        while (batch.Count < _options.BatchSize && _queue.Reader.TryRead(out var auditEvent))
        {
            batch.Add(auditEvent);
        }

        return batch;
    }

    /// <summary>
    /// 把一批事件映射成记录并一次批量写入
    /// </summary>
    /// <remarks>
    /// 整批异常或逐项被拒都只记日志并计数，这一批不再重试
    /// </remarks>
    private async Task PersistAsync(List<AuditEvent> batch, CancellationToken cancellationToken)
    {
        var records = new AuditRecord[batch.Count];
        for (var index = 0; index < batch.Count; index++)
        {
            records[index] = AuditRecordMapping.ToRecord(batch[index], _ids.NewId(), NextSequence());
        }

        IReadOnlyList<Result> results;
        try
        {
            var written = await UnitOfWorkScope.RunAsync(
                _services,
                UnitOfWorkOptions.Default,
                async (services, ct) => Result.Success(await services.GetRequiredService<IRepository<AuditRecord, Guid>>().AddRangeAsync(records, ct).ConfigureAwait(false)),
                _logger,
                cancellationToken).ConfigureAwait(false);
            // 提交返回的失败与抛出的异常一样是整批没落库：转成携带持久化错误码的异常，走下面同一条路
            results = written.ValueOrThrow();
        }
        catch (Exception exception)
        {
            // 落库失败只记日志并丢弃这一批，不重试：审计不能反过来拖垮业务调用，存储不可用时重试只会堆积
            Interlocked.Add(ref _failed, batch.Count);
            LogBatchFailed(_logger, exception, batch.Count);
            if (exception is OperationCanceledException)
            {
                throw;
            }

            return;
        }

        var rejected = 0;
        string? firstCode = null;
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                rejected++;
                firstCode ??= result.Error!.Code;
            }
        }

        if (rejected > 0)
        {
            Interlocked.Add(ref _failed, rejected);
            LogRecordsRejected(_logger, rejected, firstCode!);
        }

        Interlocked.Add(ref _persisted, batch.Count - rejected);
    }

    /// <summary>
    /// 把查询时抛出的异常归为审计存储不可用
    /// </summary>
    /// <remarks>
    /// 查询接口把这个错误返回给调用方，存储的异常消息可能带数据库信息：<c>reason</c> 只写类型全名，完整异常只进日志
    /// </remarks>
    private Error QueryFailed(Exception exception)
    {
        LogQueryFailed(_logger, exception);
        var type = exception.GetType();
        return Error.Unavailable(AuditingErrors.TrailUnavailable, AuditingMessages.TrailUnavailable, MessageArguments.Of(("reason", type.FullName ?? type.Name)));
    }

    /// <summary>
    /// 下一个写入序号
    /// </summary>
    /// <remarks>
    /// 上一个加一与时钟 UTC 刻度的较大者——同一进程内严格递增，时钟回拨或同一刻度内多次取号时退化为上一个加一
    /// </remarks>
    private long NextSequence()
    {
        _lastSequence = Math.Max(_lastSequence + 1, _clock.GetUtcNow().UtcTicks);
        return _lastSequence;
    }

    /// <summary>
    /// 按窗口节流的丢弃警告：丢弃通常成片发生，窗口内只记一条
    /// </summary>
    private void WarnDropped(string action, long dropped)
    {
        var now = _clock.GetUtcNow().UtcTicks;
        var last = Volatile.Read(ref _lastDropWarningTicks);
        if (now - last >= _dropWarningInterval.Ticks && Interlocked.CompareExchange(ref _lastDropWarningTicks, now, last) == last)
        {
            LogDropped(_logger, action, dropped);
        }
    }

    /// <summary>
    /// 一条审计事件
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Audit {Action} {Outcome} by {PrincipalId} in tenant {TenantId} on {Resource} ({TraceId}).")]
    private static partial void LogAudit(ILogger logger, string action, string outcome, string? principalId, string? tenantId, string? resource, string traceId);

    /// <summary>
    /// 事件被丢弃
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit event '{Action}' was dropped because the queue is full or the trail has stopped; {DroppedCount} events dropped so far.")]
    private static partial void LogDropped(ILogger logger, string action, long droppedCount);

    /// <summary>
    /// 整批落库失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Persisting a batch of {Count} audit events failed; the batch is dropped.")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception, int count);

    /// <summary>
    /// 批内有记录被仓储拒绝
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Count} audit records were rejected by the repository (first error '{ErrorCode}'); they are dropped.")]
    private static partial void LogRecordsRejected(ILogger logger, int count, string errorCode);

    /// <summary>
    /// 查询审计记录失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Querying the audit trail failed.")]
    private static partial void LogQueryFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 查询的只读工作单元提交失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Committing the read-only unit of work of an audit query failed with '{ErrorCode}'.")]
    private static partial void LogQueryCommitFailed(ILogger logger, string errorCode);
}
