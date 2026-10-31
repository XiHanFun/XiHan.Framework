// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp.Tests;

/// <summary>
/// 测试操作执行时记下的调用上下文要素，按到达顺序。
/// </summary>
/// <remarks>
/// 主体、租户 / 区域 / 关联 Baggage 与是否带截止时间；宿主没有登记它时操作不记。
/// </remarks>
internal sealed class CallLog
{
    /// <summary>
    /// 记录。
    /// </summary>
    private readonly ConcurrentQueue<(string Principal, string? Tenant, string? Locale, string? Correlation, bool HasDeadline)> _calls = new();

    /// <summary>
    /// 已记录的调用快照。
    /// </summary>
    public IReadOnlyList<(string Principal, string? Tenant, string? Locale, string? Correlation, bool HasDeadline)> Calls => _calls.ToList();

    /// <summary>
    /// 记下调用上下文里的要素。
    /// </summary>
    public void Record(InvocationContext context)
        => _calls.Enqueue((
            context.GetSecurity().Principal.Id,
            context.TryGetBaggage(BaggageKeys.Tenant, out var tenant) ? tenant : null,
            context.TryGetBaggage(BaggageKeys.Locale, out var locale) ? locale : null,
            context.TryGetBaggage(BaggageKeys.Correlation, out var correlation) ? correlation : null,
            context.Deadline.HasValue));
}
