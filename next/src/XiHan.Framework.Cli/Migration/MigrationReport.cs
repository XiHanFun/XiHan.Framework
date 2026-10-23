// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一次迁移的报告（<c>spec/migration/v4.md</c> §5）。
/// </summary>
/// <param name="Version">迁移来源，<c>v4</c>。</param>
/// <param name="Path">处理的根目录。</param>
/// <param name="DryRun">是否只报告不写文件。</param>
/// <param name="Rules">每条规则的结果，按编号；没有命中的也在。</param>
/// <param name="Markers">全部标记，按产生顺序。</param>
internal sealed record MigrationReport(string Version, string Path, bool DryRun, IReadOnlyList<MigrationRuleReport> Rules, IReadOnlyList<MigrationMarkerReport> Markers);
