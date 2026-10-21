// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

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
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("主体 ID 不能为空。", nameof(value));
    }

    /// <summary>
    /// API Key；至少 8 个字符。
    /// </summary>
    public required string ApiKey
    {
        get => field;
        init => field = value is { Length: >= 8 } ? value : throw new ArgumentException("API Key 至少 8 个字符。", nameof(value));
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

/// <summary>
/// 内存安全选项。配置节 <c>XiHan:Security:InMemory</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemorySecurityOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security:InMemory";

    /// <summary>
    /// 主体上限；默认 1000。
    /// </summary>
    public int MaxPrincipals
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "主体上限必须为正。");
    } = 1_000;

    /// <summary>
    /// 授予上限；默认 10000。
    /// </summary>
    public int MaxGrants
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "授予上限必须为正。");
    } = 10_000;

    /// <summary>
    /// 装载的主体。
    /// </summary>
    public IReadOnlyList<PrincipalRecord> Principals
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 装载的授予（全局）。
    /// </summary>
    public IReadOnlyList<PermissionGrant> Grants
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 从配置读取：<c>MaxPrincipals</c>、<c>MaxGrants</c>、<c>Principals:&lt;id&gt;:{ApiKey, DisplayName, Roles, Tenant, Claims:*}</c>、
    /// <c>Grants:role:&lt;role&gt;</c> 与 <c>Grants:user:&lt;id&gt;</c>（逗号分隔的权限码）。
    /// </summary>
    public static InMemorySecurityOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemorySecurityOptions();
        if (ReadPositive(configuration, "MaxPrincipals") is { } maxPrincipals)
        {
            options = options with { MaxPrincipals = maxPrincipals };
        }

        if (ReadPositive(configuration, "MaxGrants") is { } maxGrants)
        {
            options = options with { MaxGrants = maxGrants };
        }

        var principals = new List<PrincipalRecord>();
        foreach (var section in configuration.GetSection($"{ConfigurationSection}:Principals").GetChildren())
        {
            principals.Add(new PrincipalRecord
            {
                Id = section.Key,
                ApiKey = section["ApiKey"] ?? throw new ArgumentException($"{ConfigurationSection}:Principals:{section.Key}:ApiKey 不能缺失。"),
                DisplayName = section["DisplayName"],
                Roles = Split(section["Roles"]),
                Tenant = section["Tenant"],
                Claims = section.GetSection("Claims").GetChildren()
                    .Where(static c => c.Value is not null)
                    .Select(static c => new KeyValuePair<string, string>(c.Key, c.Value!))
                    .ToList(),
            });
        }

        var grants = new List<PermissionGrant>();
        foreach (var kind in new[] { GrantSubject.Role, GrantSubject.User })
        {
            foreach (var subject in configuration.GetSection($"{ConfigurationSection}:Grants:{kind}").GetChildren())
            {
                foreach (var permission in Split(subject.Value))
                {
                    grants.Add(new PermissionGrant(new GrantSubject(kind, subject.Key), permission));
                }
            }
        }

        return options with { Principals = principals, Grants = grants };
    }

    private static int? ReadPositive(IConfiguration configuration, string name)
    {
        var raw = configuration[$"{ConfigurationSection}:{name}"];
        if (raw is null)
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
            ? value
            : throw new ArgumentException($"{ConfigurationSection}:{name} 必须是正整数，实际 '{raw}'。");
    }

    private static List<string> Split(string? value)
        => value is null ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
