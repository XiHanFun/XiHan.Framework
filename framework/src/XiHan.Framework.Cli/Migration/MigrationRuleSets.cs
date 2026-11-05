// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 工具随版本发布的规则集登记表
/// </summary>
/// <remarks>
/// <c>migrate</c> 只认这里登记的来源版本
/// </remarks>
internal static class MigrationRuleSets
{
    /// <summary>
    /// 全部规则集
    /// </summary>
    /// <remarks>
    /// 当前为空；大版本升级的规则集在这里登记
    /// </remarks>
    public static IReadOnlyList<MigrationRuleSet> All { get; } = [];
}
