// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// 配置里的一个主体：API Key、显示名、角色、租户与附加声明。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PrincipalRecord
{
    /// <summary>
    /// 主体 ID。
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The principal ID must not be empty.", nameof(value));
    }

    /// <summary>
    /// API Key。
    /// </summary>
    /// <remarks>
    /// 至少 8 个字符。
    /// </remarks>
    public required string ApiKey
    {
        get => field;
        init => field = value is { Length: >= 8 } ? value : throw new ArgumentException("The API key must be at least 8 characters long.", nameof(value));
    }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 角色。
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// 所属租户。
    /// </summary>
    public string? Tenant { get; init; }

    /// <summary>
    /// 附加声明。
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Claims { get; init; } = [];

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替。
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，API 密钥不能明文出现（RFC-0014「选项里的凭据」）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对。
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("Id = ").Append(Id)
            .Append(", ApiKey = ").Append(Redaction.Redact(ApiKey))
            .Append(", DisplayName = ").Append(DisplayName)
            .Append(", Roles = ").Append(Roles)
            .Append(", Tenant = ").Append(Tenant)
            .Append(", Claims = ").Append(Claims);
        return true;
    }
}
