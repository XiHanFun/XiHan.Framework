// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

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
