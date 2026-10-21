// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 当前租户。由 Tenant 阶段写入上下文。
/// </summary>
/// <param name="Id">租户 ID。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record TenantContext(string Id)
{
    /// <summary>
    /// 租户 ID。
    /// </summary>
    public string Id { get; } = string.IsNullOrWhiteSpace(Id) ? throw new ArgumentException("租户 ID 不能为空。", nameof(Id)) : Id;

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; init; }
}
