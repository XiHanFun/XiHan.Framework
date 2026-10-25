// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 多租户：持久化子系统在新增时写入当前租户，查询时按当前租户过滤。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMultiTenant
{
    /// <summary>
    /// 租户 ID。
    /// </summary>
    string TenantId { get; set; }
}
