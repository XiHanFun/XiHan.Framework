// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 审计事件。格式是规范性内容（RFC-0001 附属），审计子系统的 Provider 负责持久化。
/// </summary>
/// <param name="Timestamp">发生时间（UTC）。</param>
/// <param name="Action">动作，例如 <c>orders.create</c>。</param>
/// <param name="Outcome">结果。</param>
/// <param name="TraceId">所属调用的 trace-id。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuditEvent(DateTimeOffset Timestamp, string Action, Result Outcome, string TraceId)
{
    /// <summary>
    /// 资源名（<c>xrn:</c>）。
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// 主体 ID。
    /// </summary>
    public string? PrincipalId { get; init; }

    /// <summary>
    /// 租户 ID。
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// 附加细节，只允许 JSON 可表示的值。
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Details { get; init; }
}

/// <summary>
/// 审计事件接收器。内核默认注册丢弃实现，审计子系统替换它。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IAuditSink
{
    /// <summary>
    /// 写入一条审计事件。实现必须有界，不得阻塞调用路径。
    /// </summary>
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>
/// 丢弃所有审计事件的接收器。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class NullAuditSink : IAuditSink
{
    /// <summary>
    /// 共享实例。
    /// </summary>
    public static NullAuditSink Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
