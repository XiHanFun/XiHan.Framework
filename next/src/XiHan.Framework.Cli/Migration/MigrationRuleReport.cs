// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一条规则的汇总。
/// </summary>
/// <param name="Id">规则编号。</param>
/// <param name="Changed">改动的语法位置数。</param>
/// <param name="Markers">插入的标记数。</param>
/// <param name="Files">涉及的文件（相对路径，字典序）。</param>
internal sealed record MigrationRuleReport(string Id, int Changed, int Markers, IReadOnlyList<string> Files);
