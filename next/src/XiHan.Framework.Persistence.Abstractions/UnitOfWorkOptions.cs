// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 开启工作单元的选项。
/// </summary>
/// <param name="TenantId">当前租户；多租户实体据此写入与过滤。</param>
/// <param name="IsReadOnly">只读：提交时不写入。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record UnitOfWorkOptions(string? TenantId = null, bool IsReadOnly = false)
{
    /// <summary>
    /// 无租户、可写。
    /// </summary>
    public static UnitOfWorkOptions Default { get; } = new();
}
