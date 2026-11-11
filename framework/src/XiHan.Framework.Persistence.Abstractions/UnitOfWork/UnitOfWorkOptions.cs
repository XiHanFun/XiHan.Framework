// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 开启工作单元的选项
/// </summary>
/// <remarks>
/// 字段都是 <c>init</c> 属性：以后加选项只加属性，构造与 <see cref="IUnitOfWork"/> 都不变（ADR-0141）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record UnitOfWorkOptions
{
    /// <summary>
    /// 无租户、可写、不绑定连接
    /// </summary>
    public static UnitOfWorkOptions Default { get; } = new();

    /// <summary>
    /// 当前租户
    /// </summary>
    /// <remarks>
    /// 多租户实体据此写入与过滤；<see langword="null"/> 为平台
    /// </remarks>
    public string? TenantId { get; init; }

    /// <summary>
    /// 只读
    /// </summary>
    /// <remarks>
    /// 不开数据库事务，仓储写入返回 <see cref="PersistenceErrors.ReadOnly"/>，Provider 的直接写入入口抛出携带它的异常。Transaction 阶段为只读操作开只读工作单元
    /// </remarks>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// 绑定的命名连接
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时按 Provider 的缺省规则（每种实体按落点取连接，事务覆盖调用触及的连接）。非空时工作单元只在这个连接上读写：仓储操作的实体必须落在它上面（否则抛出携带 <see cref="PersistenceErrors.UnknownConnection"/> 的 <see cref="FrameworkException"/>），数据库事务只开在它上面，不牵动作用域里的其他连接——管道外与业务事务互不等待的平台数据（工作流租约）用它。不支持命名连接的 Provider 在 <see cref="IUnitOfWork.BeginAsync"/> 抛出携带 <see cref="PersistenceErrors.NotSupported"/> 的异常。不能是空白
    /// </remarks>
    /// <exception cref="ArgumentException">为空白</exception>
    public string? Connection
    {
        get => field;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The connection name must not be blank.", nameof(Connection));
    }

    /// <summary>
    /// 只读工作单元也读主库
    /// </summary>
    /// <remarks>
    /// 缺省为假：配了从库的 Provider 让只读工作单元读从库。读到落后的数据会放行不该放行的调用时设它（框架的授予与租户目录读取，ADR-0122）。只读的其余语义不变：不开事务，写入照样拒绝。可写工作单元本来就读主库，没有从库的 Provider 上也没有作用
    /// </remarks>
    public bool ReadsPrimary { get; init; }
}
