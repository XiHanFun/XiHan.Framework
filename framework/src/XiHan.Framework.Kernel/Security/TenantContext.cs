// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 当前租户
/// </summary>
/// <remarks>
/// 由 Tenant 阶段写入上下文。租户 ID 是必填的初始化成员，显示名是可选的初始化成员，没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0288）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record TenantContext
{
    /// <summary>
    /// 租户 ID
    /// </summary>
    /// <exception cref="ArgumentException">为空或空白</exception>
    public required string Id
    {
        get;
        init => field = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("The tenant ID must not be empty.", nameof(value)) : value;
    }

    /// <summary>
    /// 显示名
    /// </summary>
    public string? DisplayName { get; init; }
}
