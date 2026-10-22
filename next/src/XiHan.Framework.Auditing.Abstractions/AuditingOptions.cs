// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions;

/// <summary>
/// 审计中间件的选项。从配置节 <c>XiHan:Auditing</c> 显式读取：<c>Selection</c>、<c>IncludeSuccesses</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AuditingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Auditing";

    /// <summary>
    /// 没有显式动作时记哪些操作；默认只记声明了权限的操作。
    /// </summary>
    public AuditSelection Selection { get; init; } = AuditSelection.Protected;

    /// <summary>
    /// 是否记录成功的调用；默认 <see langword="true"/>，为假时只记失败。
    /// </summary>
    public bool IncludeSuccesses { get; init; } = true;

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static AuditingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new AuditingOptions();
        if (configuration[$"{ConfigurationSection}:Selection"] is { } selection)
        {
            options = Enum.TryParse<AuditSelection>(selection, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? options with { Selection = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:Selection must be one of 'Protected', 'All' or 'None', but was '{selection}'.");
        }

        if (configuration[$"{ConfigurationSection}:IncludeSuccesses"] is { } include)
        {
            options = bool.TryParse(include, out var parsed)
                ? options with { IncludeSuccesses = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:IncludeSuccesses must be 'true' or 'false', but was '{include}'.");
        }

        return options;
    }
}
