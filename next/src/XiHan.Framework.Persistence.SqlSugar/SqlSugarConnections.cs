// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 作用域客户端里连接的 <c>ConfigId</c> 约定。
/// </summary>
/// <remarks>
/// 由特性创建客户端时默认连接是 <see cref="Default"/>，带租户分片的调用再加一个 <see cref="Shard"/> 连接；应用自己注册客户端时按同样的名字提供连接即可让 <see cref="SqlSugarConnectionSelector"/> 的默认规则生效。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class SqlSugarConnections
{
    /// <summary>
    /// 应用默认连接的 <c>ConfigId</c>：平台目录表与不带租户的实体落在这里。
    /// </summary>
    public const string Default = "default";

    /// <summary>
    /// 当前调用的租户分片连接的 <c>ConfigId</c>。
    /// </summary>
    /// <remarks>
    /// 实现 <c>IMultiTenant</c> 的实体在带分片的调用里落在这里。
    /// </remarks>
    public const string Shard = "shard";
}
