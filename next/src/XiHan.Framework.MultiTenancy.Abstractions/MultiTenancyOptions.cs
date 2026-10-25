// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 多租户选项。从配置节 <c>XiHan:MultiTenancy</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MultiTenancyOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:MultiTenancy";

    /// <summary>
    /// 没有解析到租户时使用的默认租户；<see langword="null"/> 表示没有默认租户。
    /// </summary>
    public string? DefaultTenant
    {
        get => field;
        init => field = value is null ? null : TenantIdentifier.Validate(value);
    }

    /// <summary>
    /// 调用必须带租户：没有解析到且没有默认租户时返回 <see cref="MultiTenancyErrors.MissingTenant"/>。默认 <see langword="false"/>。
    /// </summary>
    public bool RequireTenant { get; init; }

    /// <summary>
    /// 解析出的租户必须在存储中存在且启用。默认 <see langword="true"/>；关闭后只校验格式。
    /// </summary>
    public bool ValidateAgainstStore { get; init; } = true;

    /// <summary>
    /// 承载租户 ID 的声明类型；默认 <c>tenant</c>。
    /// </summary>
    public string TenantClaim
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The tenant claim type cannot be empty.", nameof(value));
    } = "tenant";

    /// <summary>
    /// 从配置读取：<c>DefaultTenant</c>、<c>RequireTenant</c>、<c>ValidateAgainstStore</c>、<c>TenantClaim</c>。
    /// </summary>
    public static MultiTenancyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new MultiTenancyOptions();
        if (configuration[$"{ConfigurationSection}:DefaultTenant"] is { } defaultTenant)
        {
            // 空字符串表示显式清除默认租户，而不是一个不合法的 ID
            options = options with { DefaultTenant = defaultTenant.Length == 0 ? null : defaultTenant };
        }

        if (ReadBool(configuration, "RequireTenant") is { } require)
        {
            options = options with { RequireTenant = require };
        }

        if (ReadBool(configuration, "ValidateAgainstStore") is { } validate)
        {
            options = options with { ValidateAgainstStore = validate };
        }

        if (configuration[$"{ConfigurationSection}:TenantClaim"] is { } claim)
        {
            options = options with { TenantClaim = claim };
        }

        return options;
    }

    /// <summary>
    /// 读取配置节下的布尔值；未配置返回 <see langword="null"/>，不是 true/false 抛出 <see cref="ArgumentException"/>。
    /// </summary>
    private static bool? ReadBool(IConfiguration configuration, string name)
    {
        var raw = configuration[$"{ConfigurationSection}:{name}"];
        if (raw is null)
        {
            return null;
        }

        return bool.TryParse(raw, out var value)
            ? value
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be 'true' or 'false', but was '{raw}'.");
    }
}
