// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Diagnostic;

/// <summary>
/// 审计事件接收器。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IAuditSink
{
    /// <summary>
    /// 写入审计事件。
    /// </summary>
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// 审计事件模型。
/// </summary>
public sealed record AuditEvent(
    string EventType,
    string? UserId,
    string? TenantId,
    string? ResourceType,
    string? ResourceId,
    string? Action,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, object?>? Metadata = null
);
