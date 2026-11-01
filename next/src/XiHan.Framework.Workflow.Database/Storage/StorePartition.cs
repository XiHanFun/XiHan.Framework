// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 存储共用的分区核对与错误归类。
/// </summary>
/// <remarks>
/// 存储在调用方的工作单元里读写：分区（存储方法的 <c>tenantId</c>）必须等于工作单元的租户——租户分区的行实现 <c>IMultiTenant</c>，持久化按工作单元的租户写入、过滤并选择分片连接，两者不一致时数据会落进别的分区，那是编程错误，抛出而不是改写。
/// </remarks>
internal static class StorePartition
{
    /// <summary>
    /// 分区必须等于调用方工作单元的租户。
    /// </summary>
    public static void Ensure(IUnitOfWork unitOfWork, string? tenantId)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        if (!string.Equals(unitOfWork.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The workflow store was asked for the partition of tenant '{tenantId}' but the unit of work belongs to tenant '{unitOfWork.TenantId}'.");
        }
    }

    /// <summary>
    /// 写入的记录属于的租户必须等于写入的分区。
    /// </summary>
    public static void EnsureOwned(string? tenantId, string? ownerTenantId, string kind, string id)
    {
        if (!string.Equals(tenantId, ownerTenantId, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The {kind} '{id}' belongs to tenant '{ownerTenantId}' but was written into the partition of tenant '{tenantId}'.", nameof(ownerTenantId));
        }
    }

    /// <summary>
    /// 失败归为存储不可达，成功原样返回。
    /// </summary>
    /// <remarks>
    /// 只读工作单元的拒绝（<see cref="PersistenceErrors.ReadOnly"/>）原样返回：它是调用方的缺陷（HTTP 403），不是存储不可达。
    /// </remarks>
    public static Result<T> OrUnavailable<T>(Result<T> result) => result.IsSuccess ? result : Unavailable(result.Error!);

    /// <summary>
    /// 失败归为存储不可达，成功原样返回。
    /// </summary>
    public static Result OrUnavailable(Result result) => result.IsSuccess ? result : Unavailable(result.Error!);

    /// <summary>
    /// 把失败归为存储不可达。
    /// </summary>
    /// <remarks>
    /// 已经是 <see cref="WorkflowErrors.StoreUnavailable"/> 或只读拒绝的原样返回，否则以原错误码为原因、原错误为内部错误包一层。
    /// </remarks>
    public static Error Unavailable(Error cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        return cause.Code is WorkflowErrors.StoreUnavailable or PersistenceErrors.ReadOnly
            ? cause
            : new Error(WorkflowErrors.StoreUnavailable, WorkflowMessages.StoreUnavailable, MessageArguments.Of(("reason", cause.Code)), cause);
    }

    /// <summary>
    /// 以文本原因构造存储不可达。
    /// </summary>
    public static Error Unavailable(string reason)
        => new(WorkflowErrors.StoreUnavailable, WorkflowMessages.StoreUnavailable, MessageArguments.Of(("reason", reason)));
}
