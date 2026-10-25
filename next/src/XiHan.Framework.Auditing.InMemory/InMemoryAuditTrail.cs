// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Auditing.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.InMemory;

/// <summary>
/// 有界的内存审计：环形缓冲保留最近 <see cref="InMemoryAuditingOptions.Capacity"/> 条事件，按写入顺序查询；每条事件可同时写日志（内核日志薄桥）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class InMemoryAuditTrail : IAuditSink, IAuditTrail
{
    /// <summary>
    /// 一条记录：序号用作续页令牌，淘汰最旧的记录后仍然有效。
    /// </summary>
    /// <param name="Sequence">序号。</param>
    /// <param name="Event">事件。</param>
    private sealed record Entry(long Sequence, AuditEvent Event);

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly InMemoryAuditingOptions _options;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 保护缓冲的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 缓冲，先进先出。
    /// </summary>
    private readonly Queue<Entry> _entries;

    /// <summary>
    /// 最近分配的序号。
    /// </summary>
    private long _sequence;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryAuditTrail(InMemoryAuditingOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = loggerFactory.CreateLogger<InMemoryAuditTrail>();
        _entries = new Queue<Entry>(Math.Min(options.Capacity, 1024));
    }

    /// <summary>
    /// 容量。
    /// </summary>
    public int Capacity => _options.Capacity;

    /// <summary>
    /// 当前保留的事件数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        lock (_lock)
        {
            // 有界：满了丢最旧的，序号继续递增，已发出的续页令牌不会指向错误的位置
            if (_entries.Count >= _options.Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(new Entry(++_sequence, auditEvent));
        }

        if (_options.LogEvents)
        {
            LogAudit(_logger, auditEvent.Action, auditEvent.Outcome.IsSuccess ? "success" : auditEvent.Outcome.Error!.Code, auditEvent.PrincipalId, auditEvent.TenantId, auditEvent.Resource, auditEvent.TraceId);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<Result<AuditPage>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var validation = query.Validate();
        if (validation.IsFailure)
        {
            return ValueTask.FromResult<Result<AuditPage>>(validation.Error!);
        }

        long after = 0;
        if (query.ContinuationToken is { } token && (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out after) || after < 0))
        {
            return ValueTask.FromResult<Result<AuditPage>>(Error.Validation(AuditingErrors.QueryInvalid, AuditingMessages.QueryInvalid, MessageArguments.Of(("reason", "continuation token is not recognized"))));
        }

        var events = new List<AuditEvent>(Math.Min(query.PageSize, 256));
        long last = 0;
        var more = false;
        lock (_lock)
        {
            foreach (var entry in _entries)
            {
                if (entry.Sequence <= after || !query.Matches(entry.Event))
                {
                    continue;
                }

                if (events.Count == query.PageSize)
                {
                    more = true;
                    break;
                }

                events.Add(entry.Event);
                last = entry.Sequence;
            }
        }

        return ValueTask.FromResult(Result.Success(new AuditPage(events, more ? last.ToString(CultureInfo.InvariantCulture) : null)));
    }

    /// <summary>
    /// 一条审计事件。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Audit {Action} {Outcome} by {PrincipalId} in tenant {TenantId} on {Resource} ({TraceId}).")]
    private static partial void LogAudit(ILogger logger, string action, string outcome, string? principalId, string? tenantId, string? resource, string traceId);
}
