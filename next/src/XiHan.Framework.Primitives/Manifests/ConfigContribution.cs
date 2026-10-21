// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 <c>contributes.config</c>：配置节与可选的 JSON Schema 路径。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record ConfigContribution
{
    /// <summary>
    /// 创建配置贡献。
    /// </summary>
    /// <param name="section">配置节，格式 <c>XiHan:&lt;领域&gt;</c>。</param>
    /// <param name="schema">JSON Schema 的相对路径。</param>
    public ConfigContribution(string section, string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!SectionPattern().IsMatch(section))
        {
            throw new ArgumentException($"配置节 '{section}' 必须是 XiHan:<领域> 形式。", nameof(section));
        }

        Section = section;
        Schema = schema;
    }

    /// <summary>
    /// 配置节。
    /// </summary>
    public string Section { get; }

    /// <summary>
    /// JSON Schema 的相对路径。
    /// </summary>
    public string? Schema { get; }

    [GeneratedRegex("^XiHan:[A-Z][A-Za-z0-9]*$")]
    private static partial Regex SectionPattern();
}
