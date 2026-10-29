// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 一个租户的数据分片。
/// </summary>
/// <remarks>
/// 租户专属的连接串、可选的数据库提供者名与透传给持久化 Provider 的属性。没有分片的租户用应用默认连接。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TenantShard
{
    /// <summary>
    /// 创建。
    /// </summary>
    public TenantShard(string tenantId, string connectionString, string? provider = null, IReadOnlyDictionary<string, string>? properties = null)
    {
        TenantId = TenantIdentifier.Validate(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
        Provider = string.IsNullOrWhiteSpace(provider) ? null : provider.ToLowerInvariant();
        Properties = properties ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 租户 ID。
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// 租户专属的连接串。
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// 数据库提供者名（小写，如 <c>postgresql</c>、<c>sqlite</c>）。
    /// </summary>
    /// <remarks>
    /// 空表示与应用默认连接同一种。
    /// </remarks>
    public string? Provider { get; }

    /// <summary>
    /// 透传给持久化 Provider 的属性（并入绑定属性）。
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; }
}
