// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli;

/// <summary>
/// 命令行工具的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>。公开只为让架构测试核对常量与语言文件一一对应。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CliMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "cli";

    /// <summary><c>pack</c> 命令说明。</summary>
    public const string PackDescription = "cli.pack.description";

    /// <summary>打包完成。参数：<c>count</c>、<c>output</c>。</summary>
    public const string PackWritten = "cli.pack.written";

    /// <summary><c>--only</c> 里有不认识的产物种类。参数：<c>kind</c>。</summary>
    public const string PackKindUnknown = "cli.pack.kind_unknown";

    /// <summary>应用清单文件不存在。参数：<c>path</c>。</summary>
    public const string ManifestNotFound = "cli.manifest.not_found";

    /// <summary>应用清单不合法。参数：<c>path</c>、<c>reason</c>。</summary>
    public const string ManifestInvalid = "cli.manifest.invalid";

    /// <summary><c>graph</c> 命令说明。</summary>
    public const string GraphDescription = "cli.graph.description";

    /// <summary><c>pipeline</c> 命令说明。</summary>
    public const string PipelineDescription = "cli.pipeline.description";

    /// <summary><c>operations</c> 命令说明。</summary>
    public const string OperationsDescription = "cli.operations.description";

    /// <summary><c>invoke</c> 命令说明。</summary>
    public const string InvokeDescription = "cli.invoke.description";

    /// <summary><c>version</c> 命令说明。</summary>
    public const string VersionDescription = "cli.version.description";

    /// <summary>无法启动 dotnet。参数：<c>reason</c>。</summary>
    public const string DotnetUnavailable = "cli.dotnet.unavailable";

    /// <summary><c>migrate</c> 命令说明。</summary>
    public const string MigrateDescription = "cli.migrate.description";

    /// <summary>来源版本没有对应的规则集。参数：<c>version</c>、<c>known</c>。</summary>
    public const string MigrateVersionUnknown = "cli.migrate.version_unknown";

    /// <summary>这一版工具没有任何迁移规则集。参数：<c>version</c>。</summary>
    public const string MigrateNoRuleSets = "cli.migrate.no_rule_sets";

    /// <summary>路径不存在。参数：<c>path</c>。</summary>
    public const string MigratePathNotFound = "cli.migrate.path_not_found";

    /// <summary>不认识的规则编号。参数：<c>fixer</c>、<c>known</c>。</summary>
    public const string MigrateFixerUnknown = "cli.migrate.fixer_unknown";

    /// <summary>表头四列，<c>|</c> 分隔。</summary>
    public const string MigrateTableHeader = "cli.migrate.table_header";

    /// <summary>汇总。参数：<c>files</c>、<c>changed</c>、<c>markers</c>。</summary>
    public const string MigrateSummary = "cli.migrate.summary";

    /// <summary>试运行提示。</summary>
    public const string MigrateDryRun = "cli.migrate.dry_run";

    /// <summary>报告已写出。参数：<c>path</c>。</summary>
    public const string MigrateReportWritten = "cli.migrate.report_written";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(CliMessages));
}
