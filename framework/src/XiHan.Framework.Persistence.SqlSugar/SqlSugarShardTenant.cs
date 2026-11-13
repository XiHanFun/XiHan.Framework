// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 作用域的客户端建在哪个租户的分片上
/// </summary>
/// <remarks>
/// 由特性建的客户端工厂在建出带分片连接的客户端时写入，工作单元开始时据此核对它的租户（ADR-0223）。应用自己登记客户端时不写入，不核对
/// </remarks>
internal sealed class SqlSugarShardTenant
{
    /// <summary>
    /// 客户端是否带分片连接
    /// </summary>
    public bool IsBound { get; set; }

    /// <summary>
    /// 分片的租户
    /// </summary>
    public string? TenantId { get; set; }
}
