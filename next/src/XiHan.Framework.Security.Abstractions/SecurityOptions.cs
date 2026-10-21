// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

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
