// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Migration;

namespace XiHan.Framework.Cli.Tests.Migration;

/// <summary>
/// 测试用的规则集登记：来源版本 <c>t5</c>，三条规则按编号执行。
/// </summary>
internal static class TestRuleSets
{
    /// <summary>
    /// 来源版本 <c>t5</c> 的规则集。
    /// </summary>
    public static MigrationRuleSet T5 { get; } = new("t5", [new RenameClockFixer(), new InventoryFixer(), new LegacyJsonFixer()]);

    /// <summary>
    /// 交给命令的规则集表。
    /// </summary>
    public static IReadOnlyList<MigrationRuleSet> All { get; } = [T5];
}
