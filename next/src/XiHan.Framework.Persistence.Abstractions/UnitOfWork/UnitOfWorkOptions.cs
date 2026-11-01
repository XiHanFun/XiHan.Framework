// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 开启工作单元的选项。
/// </summary>
/// <param name="TenantId">当前租户；多租户实体据此写入与过滤。</param>
/// <param name="IsReadOnly">只读：不开数据库事务，仓储写入返回 <see cref="PersistenceErrors.ReadOnly"/>，Provider 的直接写入入口抛出携带它的异常。Transaction 阶段为只读操作开只读工作单元。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record UnitOfWorkOptions(string? TenantId = null, bool IsReadOnly = false)
{
    /// <summary>
    /// 无租户、可写。
    /// </summary>
    public static UnitOfWorkOptions Default { get; } = new();
}
