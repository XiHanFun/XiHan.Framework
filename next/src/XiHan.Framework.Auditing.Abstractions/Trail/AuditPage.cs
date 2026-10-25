// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Trail;

/// <summary>
/// 查询的一页。
/// </summary>
/// <param name="Events">事件，按写入顺序升序。</param>
/// <param name="ContinuationToken">取下一页的不透明令牌；没有下一页为 <see langword="null"/>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AuditPage(IReadOnlyList<AuditEvent> Events, string? ContinuationToken);
