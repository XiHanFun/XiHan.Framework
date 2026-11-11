// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Kernel.Diagnostics;

namespace XiHan.Framework.Auditing.Database.Tests.Fixtures;

/// <summary>
/// 套件用的接收器装饰器
/// </summary>
/// <remarks>
/// 写入转给 <see cref="DatabaseAuditTrail"/> 后立刻刷写并等待落库，让套件"写入后立即查询"的前提成立
/// </remarks>
internal sealed class FlushingAuditSink : IAuditSink
{
    /// <summary>
    /// 被装饰的数据库审计
    /// </summary>
    private readonly DatabaseAuditTrail _trail;

    /// <summary>
    /// 创建
    /// </summary>
    public FlushingAuditSink(DatabaseAuditTrail trail)
    {
        _trail = trail;
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await _trail.WriteAsync(auditEvent, cancellationToken);
        // 接收器契约不抛出（ADR-0147）：刷盘是夹具为了让查询立刻可见，不随调用方的令牌取消
        await _trail.FlushAsync(CancellationToken.None);
    }
}
