// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 <c>contributes.config</c>：配置节与可选的 JSON Schema 路径。
/// </summary>
/// <remarks>
/// 配置节是领域节 <c>XiHan:&lt;领域&gt;</c>，可再带一层或多层子节，如 <c>XiHan:Caching:Redis</c>（RFC-0002）。特性声明它在本领域里读取的配置的最近公共节：只读自己子节的声明子节，同时读领域节下的公共选项或几个子节的声明领域节。插件宿主在 <c>Configure</c> 时把这一整节下发给插件。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record ConfigContribution
{
    /// <summary>
    /// 创建配置贡献。
    /// </summary>
    /// <param name="section">配置节，格式 <c>XiHan:&lt;领域&gt;</c> 或 <c>XiHan:&lt;领域&gt;:&lt;子节&gt;…</c>。</param>
    /// <param name="schema">JSON Schema 的相对路径。</param>
    public ConfigContribution(string section, string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!SectionPattern().IsMatch(section))
        {
            throw new ArgumentException($"Configuration section '{section}' must be of the form 'XiHan:<Domain>' or 'XiHan:<Domain>:<Section>...', with PascalCase segments.", nameof(section));
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

    /// <summary>
    /// 配置节格式的正则。
    /// </summary>
    /// <remarks>
    /// <c>XiHan:</c> 前缀加 PascalCase 领域词，其后可接任意层 PascalCase 子节。
    /// </remarks>
    [GeneratedRegex("^XiHan:[A-Z][A-Za-z0-9]*(:[A-Z][A-Za-z0-9]*)*$")]
    private static partial Regex SectionPattern();
}
