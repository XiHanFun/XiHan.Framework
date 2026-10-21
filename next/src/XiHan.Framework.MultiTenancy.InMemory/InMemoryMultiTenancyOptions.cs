// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.InMemory;

/// <summary>
/// 内存租户存储选项：容量与从配置装载的租户。配置节 <c>XiHan:MultiTenancy:InMemory</c> 与 <c>XiHan:MultiTenancy:Tenants</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemoryMultiTenancyOptions
{
    /// <summary>
    /// 容量配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:MultiTenancy:InMemory";

    /// <summary>
    /// 租户列表配置节。
    /// </summary>
    public const string TenantsSection = "XiHan:MultiTenancy:Tenants";

    /// <summary>
    /// 租户上限；默认 1000。
    /// </summary>
    public int MaxTenants
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "租户上限必须为正。");
    } = 1_000;

    /// <summary>
    /// 启动时装载的租户。
    /// </summary>
    public IReadOnlyList<TenantInfo> Tenants
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 从配置读取。每个租户是 <c>Tenants:&lt;id&gt;</c> 下的节：<c>DisplayName</c>、<c>IsEnabled</c>、<c>Properties:*</c>。
    /// </summary>
    public static InMemoryMultiTenancyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemoryMultiTenancyOptions();
        var max = configuration[$"{ConfigurationSection}:MaxTenants"];
        if (max is not null)
        {
            options = int.TryParse(max, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { MaxTenants = value }
                : throw new ArgumentException($"{ConfigurationSection}:MaxTenants 必须是正整数，实际 '{max}'。");
        }

        var tenants = new List<TenantInfo>();
        foreach (var section in configuration.GetSection(TenantsSection).GetChildren())
        {
            var enabledRaw = section["IsEnabled"];
            var enabled = enabledRaw is null
                ? true
                : bool.TryParse(enabledRaw, out var parsed) ? parsed : throw new ArgumentException($"{TenantsSection}:{section.Key}:IsEnabled 必须是 true 或 false，实际 '{enabledRaw}'。");
            var properties = section.GetSection("Properties").GetChildren()
                .Where(static p => p.Value is not null)
                .ToDictionary(static p => p.Key, static p => p.Value!, StringComparer.Ordinal);
            tenants.Add(new TenantInfo
            {
                Id = section.Key,
                DisplayName = section["DisplayName"],
                IsEnabled = enabled,
                Properties = properties,
            });
        }

        return options with { Tenants = tenants };
    }
}
