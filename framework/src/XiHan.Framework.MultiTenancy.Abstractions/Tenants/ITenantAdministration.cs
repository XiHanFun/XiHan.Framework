// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tenants;

/// <summary>
/// 租户的管理：批量新增或替换、批量删除
/// </summary>
/// <remarks>
/// 可选接口，与只读的 <see cref="ITenantStore"/> 分开；列出用 <see cref="ITenantStore.ListAsync"/>。逐项结果与入参同序（批量优先第 4 条），空集合不访问后端；写入后让租户目录与分片解析器丢弃这些租户的缓存，同一实例上随后的解析看见新值（ADR-0160）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ITenantAdministration
{
    /// <summary>
    /// 新增或整份替换，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 替换是整份覆盖：没给的显示名与属性被清空。新增超出容量返回 <see cref="MultiTenancyErrors.CapacityExceeded"/>；同一批里同一 ID 出现多次，后一项覆盖前一项
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> UpsertAsync(IReadOnlyList<TenantInfo> tenants, CancellationToken cancellationToken);

    /// <summary>
    /// 删除，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 不存在返回 <see cref="MultiTenancyErrors.NotFound"/>，同一批里同一 ID 第二次同样不存在
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> RemoveAsync(IReadOnlyList<string> tenantIds, CancellationToken cancellationToken);
}
