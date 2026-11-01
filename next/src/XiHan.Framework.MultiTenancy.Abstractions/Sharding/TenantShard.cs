// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
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

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替。
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，连接串不能明文出现（RFC-0014「选项里的凭据」）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对。
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("TenantId = ").Append(TenantId)
            .Append(", ConnectionString = ").Append(Redaction.Redact(ConnectionString))
            .Append(", Provider = ").Append(Provider)
            .Append(", Properties = ").Append(Properties);
        return true;
    }
}
