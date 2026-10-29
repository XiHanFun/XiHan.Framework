// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 工具随版本发布的规则集登记表。
/// </summary>
/// <remarks>
/// <c>migrate</c> 只认这里登记的来源版本。
/// </remarks>
internal static class MigrationRuleSets
{
    /// <summary>
    /// 全部规则集。
    /// </summary>
    /// <remarks>
    /// 当前为空：4.x 单独维护、不提供迁移规则（ADR-0021），5.x 之后的大版本升级在这里登记。
    /// </remarks>
    public static IReadOnlyList<MigrationRuleSet> All { get; } = [];
}
