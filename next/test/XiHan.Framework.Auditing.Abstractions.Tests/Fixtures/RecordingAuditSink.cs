// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Diagnostics;

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 记录每条事件的接收器；可让写入抛出。
/// </summary>
internal sealed class RecordingAuditSink : IAuditSink
{
    /// <summary>
    /// 保护记录的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 记录。
    /// </summary>
    private readonly List<AuditEvent> _events = [];

    /// <summary>
    /// 为真时写入抛出。
    /// </summary>
    public bool Throw { get; set; }

    /// <summary>
    /// 已记录的事件快照。
    /// </summary>
    public IReadOnlyList<AuditEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToList();
            }
        }
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        if (Throw)
        {
            throw new InvalidOperationException("The audit sink is broken.");
        }

        lock (_lock)
        {
            _events.Add(auditEvent);
        }

        return ValueTask.CompletedTask;
    }
}
