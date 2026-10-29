// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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
}
