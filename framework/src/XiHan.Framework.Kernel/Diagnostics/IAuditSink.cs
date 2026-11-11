// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 审计事件接收器
/// </summary>
/// <remarks>
/// 内核默认注册丢弃实现，审计子系统替换它。接收器与日志接收器同一模型：调用方交出事件就继续，接收器自己负责落地与报告故障（ADR-0147）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IAuditSink
{
    /// <summary>
    /// 接收一条审计事件
    /// </summary>
    /// <remarks>
    /// 契约：
    /// <list type="bullet">
    /// <item>有界、不阻塞调用路径：不等待存储，返回只表示已接收，不表示已持久化</item>
    /// <item>不抛出：接收不下（队列满、已停止）或落地失败时由实现自己丢弃并计数、节流告警，最好经健康贡献条目暴露；只有 <paramref name="auditEvent"/> 为 <see langword="null"/> 这类编程错误抛出 <see cref="ArgumentNullException"/></item>
    /// <item>取消：Observe 阶段传 <see cref="CancellationToken.None"/>，被取消的调用同样要留痕；令牌被取消时实现可以放弃这条事件，但仍不抛出</item>
    /// </list>
    /// Observe 阶段仍会兜住抛出的异常并记一条 Error 日志，那是实现缺陷，不改变调用结果
    /// </remarks>
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
