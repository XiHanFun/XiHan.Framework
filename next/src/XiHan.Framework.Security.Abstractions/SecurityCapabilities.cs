// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 安全子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityCapabilities
{
    /// <summary>认证：凭据到主体。</summary>
    public const string Authentication = "security.authentication";

    /// <summary>授权：策略判定与权限授予。</summary>
    public const string Authorization = "security.authorization";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}

/// <summary>
/// 安全错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityErrors
{
    /// <summary>未认证。</summary>
    public const string NotAuthenticated = "SECURITY_DENIED_001";

    /// <summary>策略拒绝。</summary>
    public const string PolicyDenied = "SECURITY_DENIED_002";

    /// <summary>凭据无效、过期或签名不符。</summary>
    public const string InvalidCredential = "SECURITY_DENIED_003";

    /// <summary>缺少权限。</summary>
    public const string PermissionMissing = "SECURITY_DENIED_004";

    /// <summary>不支持的认证方案或凭据格式不合法。</summary>
    public const string UnsupportedScheme = "SECURITY_VALIDATION_001";

    /// <summary>策略不存在或权限码未定义。</summary>
    public const string PolicyNotFound = "SECURITY_NOTFOUND_001";

    /// <summary>身份或授予存储不可达。</summary>
    public const string StoreUnavailable = "SECURITY_UNAVAILABLE_001";

    /// <summary>存储容量已满。</summary>
    public const string CapacityExceeded = "SECURITY_UNAVAILABLE_002";
}

/// <summary>
/// 安全选项。从配置节 <c>XiHan:Security</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record SecurityOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security";

    /// <summary>
    /// 为真时匿名调用在 Authenticate 阶段被拒绝（<see cref="SecurityErrors.NotAuthenticated"/>）。默认 <see langword="false"/>：由策略决定。
    /// </summary>
    public bool RequireAuthentication { get; init; }

    /// <summary>
    /// 承载角色的声明类型；默认 <c>role</c>。
    /// </summary>
    public string RoleClaim
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("角色声明类型不能为空。", nameof(value));
    } = "role";

    /// <summary>
    /// 从配置读取：<c>RequireAuthentication</c>、<c>RoleClaim</c>。
    /// </summary>
    public static SecurityOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new SecurityOptions();
        var require = configuration[$"{ConfigurationSection}:RequireAuthentication"];
        if (require is not null)
        {
            options = bool.TryParse(require, out var value)
                ? options with { RequireAuthentication = value }
                : throw new ArgumentException($"{ConfigurationSection}:RequireAuthentication 必须是 true 或 false，实际 '{require}'。");
        }

        if (configuration[$"{ConfigurationSection}:RoleClaim"] is { } roleClaim)
        {
            options = options with { RoleClaim = roleClaim };
        }

        return options;
    }
}
