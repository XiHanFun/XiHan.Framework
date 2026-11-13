// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli;

/// <summary>
/// 命令行工具的消息键
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>。公开只为让架构测试核对常量与语言文件一一对应
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class CliMessages
{
    /// <summary>
    /// 目录名
    /// </summary>
    public const string CatalogName = "cli";

    /// <summary>
    /// <c>pack</c> 命令说明
    /// </summary>
    public const string PackDescription = "cli.pack.description";

    /// <summary>
    /// 打包完成
    /// </summary>
    /// <remarks>
    /// 参数：<c>count</c>、<c>output</c>
    /// </remarks>
    public const string PackWritten = "cli.pack.written";

    /// <summary>
    /// <c>--only</c> 里有不认识的产物种类
    /// </summary>
    /// <remarks>
    /// 参数：<c>kind</c>
    /// </remarks>
    public const string PackKindUnknown = "cli.pack.kind_unknown";

    /// <summary>
    /// 进 ConfigMap 的配置文件里有带值的凭据
    /// </summary>
    /// <remarks>
    /// 参数：<c>file</c>、<c>keys</c>（逗号分隔的配置路径，不带值）
    /// </remarks>
    public const string PackConfigSecret = "cli.pack.config_secret";

    /// <summary>
    /// 应用清单文件不存在
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string ManifestNotFound = "cli.manifest.not_found";

    /// <summary>
    /// 应用清单不合法
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>、<c>reason</c>
    /// </remarks>
    public const string ManifestInvalid = "cli.manifest.invalid";

    /// <summary>
    /// <c>graph</c> 命令说明
    /// </summary>
    public const string GraphDescription = "cli.graph.description";

    /// <summary>
    /// <c>pipeline</c> 命令说明
    /// </summary>
    public const string PipelineDescription = "cli.pipeline.description";

    /// <summary>
    /// <c>operations</c> 命令说明
    /// </summary>
    public const string OperationsDescription = "cli.operations.description";

    /// <summary>
    /// <c>invoke</c> 命令说明
    /// </summary>
    public const string InvokeDescription = "cli.invoke.description";

    /// <summary>
    /// <c>version</c> 命令说明
    /// </summary>
    public const string VersionDescription = "cli.version.description";

    /// <summary>
    /// 无法启动 dotnet
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>
    /// </remarks>
    public const string DotnetUnavailable = "cli.dotnet.unavailable";

    /// <summary>
    /// <c>migrate</c> 命令说明
    /// </summary>
    public const string MigrateDescription = "cli.migrate.description";

    /// <summary>
    /// 来源版本没有对应的规则集
    /// </summary>
    /// <remarks>
    /// 参数：<c>version</c>、<c>known</c>
    /// </remarks>
    public const string MigrateVersionUnknown = "cli.migrate.version_unknown";

    /// <summary>
    /// 这一版工具没有任何迁移规则集
    /// </summary>
    /// <remarks>
    /// 参数：<c>version</c>
    /// </remarks>
    public const string MigrateNoRuleSets = "cli.migrate.no_rule_sets";

    /// <summary>
    /// 路径不存在
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string MigratePathNotFound = "cli.migrate.path_not_found";

    /// <summary>
    /// 不认识的规则编号
    /// </summary>
    /// <remarks>
    /// 参数：<c>fixer</c>、<c>known</c>
    /// </remarks>
    public const string MigrateFixerUnknown = "cli.migrate.fixer_unknown";

    /// <summary>
    /// 表头四列，<c>|</c> 分隔
    /// </summary>
    public const string MigrateTableHeader = "cli.migrate.table_header";

    /// <summary>
    /// 汇总
    /// </summary>
    /// <remarks>
    /// 参数：<c>files</c>、<c>changed</c>、<c>markers</c>
    /// </remarks>
    public const string MigrateSummary = "cli.migrate.summary";

    /// <summary>
    /// 试运行提示
    /// </summary>
    public const string MigrateDryRun = "cli.migrate.dry_run";

    /// <summary>
    /// 报告已写出
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string MigrateReportWritten = "cli.migrate.report_written";

    /// <summary>
    /// <c>mcp</c> 命令说明
    /// </summary>
    public const string McpDescription = "cli.mcp.description";

    /// <summary>
    /// MCP 服务端的使用说明
    /// </summary>
    /// <remarks>
    /// 参数：<c>version</c>
    /// </remarks>
    public const string McpInstructions = "cli.mcp.instructions";

    /// <summary>
    /// 项目引用的框架版本与工具不同
    /// </summary>
    /// <remarks>
    /// 参数：<c>project</c>、<c>version</c>
    /// </remarks>
    public const string McpVersionMismatch = "cli.mcp.version_mismatch";

    /// <summary>
    /// tool <c>xihan_framework_info</c> 的标题
    /// </summary>
    /// <remarks>
    /// 键由操作 ID <c>cli.info</c> 加 <c>.title</c> 构成，<c>Protocols.Mcp</c> 按它取标题
    /// </remarks>
    public const string InfoTitle = "cli.info.title";

    /// <summary>
    /// tool <c>xihan_framework_info</c> 的描述
    /// </summary>
    public const string InfoDescription = "cli.info.description";

    /// <summary>
    /// tool <c>xihan_framework_config</c> 的标题
    /// </summary>
    public const string ConfigTitle = "cli.config.title";

    /// <summary>
    /// tool <c>xihan_framework_config</c> 的描述
    /// </summary>
    public const string ConfigDescription = "cli.config.description";

    /// <summary>
    /// tool <c>xihan_framework_search</c> 的标题
    /// </summary>
    public const string SearchTitle = "cli.search.title";

    /// <summary>
    /// tool <c>xihan_framework_search</c> 的描述
    /// </summary>
    public const string SearchDescription = "cli.search.description";

    /// <summary>
    /// 知识库里查不到
    /// </summary>
    /// <remarks>
    /// 参数：<c>subject</c>
    /// </remarks>
    public const string KnowledgeNotFound = "cli.knowledge.not_found";

    /// <summary>
    /// 知识库里查不到，附最接近的候选
    /// </summary>
    /// <remarks>
    /// 参数：<c>subject</c>、<c>candidates</c>
    /// </remarks>
    public const string KnowledgeNotFoundCandidates = "cli.knowledge.not_found_candidates";

    /// <summary>
    /// 包不读取任何配置节
    /// </summary>
    /// <remarks>
    /// 参数：<c>package</c>
    /// </remarks>
    public const string KnowledgeNoSections = "cli.knowledge.no_sections";

    /// <summary>
    /// 检索既没给检索词也没给页面
    /// </summary>
    public const string KnowledgeQueryRequired = "cli.knowledge.query_required";

    /// <summary>
    /// 检索条数超出范围
    /// </summary>
    /// <remarks>
    /// 参数：<c>limit</c>、<c>max</c>
    /// </remarks>
    public const string KnowledgeLimitInvalid = "cli.knowledge.limit_invalid";

    /// <summary>
    /// <c>setup</c> 命令说明
    /// </summary>
    public const string SetupDescription = "cli.setup.description";

    /// <summary>
    /// <c>--client</c> 不认识
    /// </summary>
    /// <remarks>
    /// 参数：<c>client</c>、<c>known</c>
    /// </remarks>
    public const string SetupClientInvalid = "cli.setup.client_invalid";

    /// <summary>
    /// <c>--mode</c> 不认识
    /// </summary>
    /// <remarks>
    /// 参数：<c>mode</c>、<c>known</c>
    /// </remarks>
    public const string SetupModeInvalid = "cli.setup.mode_invalid";

    /// <summary>
    /// 找不到项目根目录
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string SetupRootNotFound = "cli.setup.root_not_found";

    /// <summary>
    /// 编码助手的配置文件不是合法 JSON
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>、<c>reason</c>
    /// </remarks>
    public const string SetupConfigInvalid = "cli.setup.config_invalid";

    /// <summary>
    /// 编码助手的配置文件顶层不是对象
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string SetupConfigNotObject = "cli.setup.config_not_object";

    /// <summary>
    /// 已把服务写进 JSON 配置
    /// </summary>
    /// <remarks>
    /// 参数：<c>server</c>、<c>key</c>、<c>path</c>
    /// </remarks>
    public const string SetupJsonWritten = "cli.setup.json_written";

    /// <summary>
    /// 试运行：将把服务写进 JSON 配置
    /// </summary>
    /// <remarks>
    /// 参数：<c>server</c>、<c>key</c>、<c>path</c>
    /// </remarks>
    public const string SetupJsonPlanned = "cli.setup.json_planned";

    /// <summary>
    /// 已写入文件
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string SetupFileWritten = "cli.setup.file_written";

    /// <summary>
    /// 试运行：将写入文件
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string SetupFilePlanned = "cli.setup.file_planned";

    /// <summary>
    /// 请把片段加进配置
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string SetupPrint = "cli.setup.print";

    /// <summary>
    /// <c>--check</c>：配置已就绪
    /// </summary>
    /// <remarks>
    /// 参数：<c>client</c>、<c>version</c>
    /// </remarks>
    public const string SetupReady = "cli.setup.ready";

    /// <summary>
    /// <c>--check</c>：配置未就绪
    /// </summary>
    /// <remarks>
    /// 参数：<c>client</c>、<c>problems</c>
    /// </remarks>
    public const string SetupNotReady = "cli.setup.not_ready";

    /// <summary>
    /// 未就绪的一项：缺少文件
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string SetupMissingFile = "cli.setup.missing_file";

    /// <summary>
    /// 未就绪的一项：配置里没有本服务
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>、<c>server</c>
    /// </remarks>
    public const string SetupMissingEntry = "cli.setup.missing_entry";

    /// <summary>
    /// 未就绪的一项：本服务的配置与期望不同
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>、<c>server</c>
    /// </remarks>
    public const string SetupEntryDiffers = "cli.setup.entry_differs";

    /// <summary>
    /// 未就绪的一项：技能文件与当前版本不同
    /// </summary>
    /// <remarks>
    /// 参数：<c>path</c>
    /// </remarks>
    public const string SetupSkillDiffers = "cli.setup.skill_differs";

    /// <summary>
    /// 本包的消息目录
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(CliMessages));
}
