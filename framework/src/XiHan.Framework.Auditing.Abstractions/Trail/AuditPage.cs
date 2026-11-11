// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Trail;

/// <summary>
/// 审计事件的一页
/// </summary>
/// <remarks>
/// 成员经初始化器给出，与其他续页分页同名（ADR-0154）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuditPage
{
    /// <summary>
    /// 本页的事件
    /// </summary>
    /// <remarks>
    /// 按写入顺序升序
    /// </remarks>
    public required IReadOnlyList<AuditEvent> Items { get; init; }

    /// <summary>
    /// 取下一页的不透明令牌
    /// </summary>
    /// <remarks>
    /// 没有下一页为 <see langword="null"/>
    /// </remarks>
    public string? ContinuationToken { get; init; }
}
