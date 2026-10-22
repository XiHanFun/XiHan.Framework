// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

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
