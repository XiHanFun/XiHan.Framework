// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 分区里的一条记录的键：分区（平台为空串）加记录 ID。
/// </summary>
/// <remarks>
/// 合法的租户 ID 不是空串（RFC-0007），空串留给平台不会撞上租户。各分区的 ID 互不相干：一个分区里的写入碰不到另一个分区里同 ID 的记录。
/// </remarks>
/// <param name="Tenant">分区：租户 ID，平台为空串。</param>
/// <param name="Id">记录 ID。</param>
internal readonly record struct PartitionKey(string Tenant, string Id)
{
    /// <summary>
    /// 分区名：租户 ID，平台（空）为空串。
    /// </summary>
    public static string Of(string? tenantId) => tenantId ?? string.Empty;

    /// <summary>
    /// 分区里某条记录的键。
    /// </summary>
    public static PartitionKey In(string? tenantId, string id) => new(Of(tenantId), id);

    /// <summary>
    /// 写入的记录属于的租户必须等于写入的分区：不一致是调用方的编程错误，不替它改写。
    /// </summary>
    public static void EnsureOwned(string? tenantId, string? ownerTenantId, string kind, string id)
    {
        if (!string.Equals(tenantId, ownerTenantId, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The {kind} '{id}' belongs to tenant '{ownerTenantId}' but was written into the partition of tenant '{tenantId}'.", nameof(ownerTenantId));
        }
    }
}
