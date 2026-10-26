// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一个来源版本的规则集（<c>spec/migration/&lt;来源版本&gt;.md</c>）。
/// </summary>
/// <param name="Version">来源版本，即 <c>migrate</c> 的第一个参数（如 <c>v5</c>），匹配时不区分大小写。</param>
/// <param name="Fixers">规则实例，按执行顺序；规则无状态，结果都记在会话里。</param>
internal sealed record MigrationRuleSet(string Version, IReadOnlyList<MigrationFixer> Fixers)
{
    /// <summary>
    /// 规则编号，去重后按执行顺序。
    /// </summary>
    public IReadOnlyList<string> Ids { get; } = Fixers.Select(static fixer => fixer.Id).Distinct(StringComparer.Ordinal).ToList();
}
