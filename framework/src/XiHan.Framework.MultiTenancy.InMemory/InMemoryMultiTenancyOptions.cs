// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.InMemory;

/// <summary>
/// 内存租户存储选项
/// </summary>
/// <remarks>
/// 容量与从配置装载的租户。配置节 <c>XiHan:MultiTenancy:InMemory</c> 与 <c>XiHan:MultiTenancy:Tenants</c>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record InMemoryMultiTenancyOptions
{
    /// <summary>
    /// 容量配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:MultiTenancy:InMemory";

    /// <summary>
    /// 租户列表配置节
    /// </summary>
    public const string TenantsSection = "XiHan:MultiTenancy:Tenants";

    /// <summary>
    /// 租户上限
    /// </summary>
    /// <remarks>
    /// 默认 1000
    /// </remarks>
    public int MaxTenants
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The tenant limit must be positive.");
    } = 1_000;

    /// <summary>
    /// 启动时装载的租户
    /// </summary>
    public IReadOnlyList<TenantInfo> Tenants
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 每个租户是 <c>Tenants:&lt;id&gt;</c> 下的节：<c>DisplayName</c>、<c>IsEnabled</c>、<c>Properties:*</c>
    /// </remarks>
    public static InMemoryMultiTenancyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemoryMultiTenancyOptions();
        if (ConfigurationValues.ReadInt32(configuration, $"{ConfigurationSection}:MaxTenants", 1) is { } max)
        {
            options = options with { MaxTenants = max };
        }

        // 每个子节的节名就是租户 ID，文法由 TenantInfo 构造时校验；缺省 IsEnabled 视为启用
        var tenants = new List<TenantInfo>();
        foreach (var section in configuration.GetSection(TenantsSection).GetChildren())
        {
            var enabled = ConfigurationValues.ReadBoolean(section, "IsEnabled") ?? true;
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
