// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 租户信息 <c>Properties</c> 里描述分片的键（<see cref="TenantStoreShardResolver"/> 读取）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class TenantShardKeys
{
    /// <summary>
    /// 租户专属连接串。
    /// </summary>
    /// <remarks>
    /// 有值即表示该租户分库。
    /// </remarks>
    public const string ConnectionString = "ConnectionString";

    /// <summary>
    /// 数据库提供者名（可选）。
    /// </summary>
    public const string DatabaseProvider = "DatabaseProvider";

    /// <summary>
    /// 透传属性的前缀。
    /// </summary>
    /// <remarks>
    /// <c>Shard.&lt;key&gt;</c> 进 <see cref="TenantShard.Properties"/> 的 <c>&lt;key&gt;</c>（用点不用冒号：租户属性来自配置时冒号会被当作节分隔符）。
    /// </remarks>
    public const string PropertyPrefix = "Shard.";

    /// <summary>
    /// 分片连接串进绑定属性时的租户键。
    /// </summary>
    public const string TenantProperty = "tenant";

    /// <summary>
    /// 分片提供者名进绑定属性时的键。
    /// </summary>
    public const string ProviderProperty = "provider";
}
