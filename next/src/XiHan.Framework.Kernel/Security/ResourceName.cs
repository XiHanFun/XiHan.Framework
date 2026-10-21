// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 统一资源名：<c>xrn:&lt;partition&gt;:&lt;service&gt;:&lt;tenant&gt;:&lt;type&gt;/&lt;id&gt;</c>，用于策略判定与审计。
/// </summary>
/// <param name="Partition">分区，例如 <c>cn</c>。</param>
/// <param name="Service">服务。</param>
/// <param name="Tenant">租户；全局资源为空字符串。</param>
/// <param name="Type">资源类型。</param>
/// <param name="Id">资源 ID。</param>
[ApiLevel(Stability.Stable, "5.0")]
public readonly partial record struct ResourceName(string Partition, string Service, string Tenant, string Type, string Id)
{
    /// <summary>
    /// 解析资源名。
    /// </summary>
    public static ResourceName Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var name) ? name : throw new ArgumentException($"'{text}' is not a valid resource name; expected 'xrn:<partition>:<service>:<tenant>:<type>/<id>'.", nameof(text));
    }

    /// <summary>
    /// 尝试解析资源名。
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out ResourceName name)
    {
        name = default;
        if (text is null)
        {
            return false;
        }

        var match = Pattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        name = new ResourceName(match.Groups["p"].Value, match.Groups["s"].Value, match.Groups["t"].Value, match.Groups["type"].Value, match.Groups["id"].Value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"xrn:{Partition}:{Service}:{Tenant}:{Type}/{Id}";

    /// <summary>
    /// 资源名格式的正则：租户段允许为空（全局资源），ID 段不含斜杠与空白。
    /// </summary>
    [GeneratedRegex("^xrn:(?<p>[a-z0-9-]+):(?<s>[a-z0-9-]+):(?<t>[a-z0-9-]*):(?<type>[a-z0-9-]+)/(?<id>[^/\\s]+)$")]
    private static partial Regex Pattern();
}
