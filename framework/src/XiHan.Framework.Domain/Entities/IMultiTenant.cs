// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 多租户：持久化子系统在新增时写入当前租户，查询时按当前租户过滤
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMultiTenant
{
    /// <summary>
    /// 租户 ID
    /// </summary>
    /// <remarks>
    /// 已持久化的实体一定有值，所以不可空；新建实体在新增前以 <see cref="string.Empty"/> 占位，新增时由持久化写入当前租户，调用方写的值不生效（ADR-0145）
    /// </remarks>
    string TenantId { get; set; }
}
