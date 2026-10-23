// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
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

    /// <summary>迁移来源不支持。参数：<c>version</c>。</summary>
    public const string MigrateVersionUnknown = "cli.migrate.version_unknown";

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

    /// <summary>生成文件的头注释。</summary>
    public const string MigrateGenerated = "cli.migrate.generated";

    /// <summary>V4-05 标记：工作单元管理器。</summary>
    public const string MigrateMarkerUnitOfWorkManager = "cli.migrate.marker.unit_of_work_manager";

    /// <summary>V4-01 标记：应用构建器。</summary>
    public const string MigrateMarkerApplicationBuilder = "cli.migrate.marker.application_builder";

    /// <summary>V4-01 标记：关闭钩子的上下文。</summary>
    public const string MigrateMarkerShutdownContext = "cli.migrate.marker.shutdown_context";

    /// <summary>V4-01 标记：异步的服务配置钩子。</summary>
    public const string MigrateMarkerAsyncConfigure = "cli.migrate.marker.async_configure";

    /// <summary>V4-01 标记：删除了无能力的 4.x 模块。参数：<c>modules</c>。</summary>
    public const string MigrateMarkerModulesDropped = "cli.migrate.marker.modules_dropped";

    /// <summary>V4-02 标记：并发戳列类型。</summary>
    public const string MigrateMarkerConcurrencyStampColumn = "cli.migrate.marker.concurrency_stamp_column";

    /// <summary>V4-02 标记：租户 ID 类型。</summary>
    public const string MigrateMarkerTenantIdType = "cli.migrate.marker.tenant_id_type";

    /// <summary>V4-02 标记：没有对应物的接口。参数：<c>name</c>。</summary>
    public const string MigrateMarkerAppInterface = "cli.migrate.marker.app_interface";

    /// <summary>V4-03 标记：没法自动注册的服务类（泛型、抽象或不在项目里）。参数：<c>name</c>。</summary>
    public const string MigrateMarkerManualRegistration = "cli.migrate.marker.manual_registration";

    /// <summary>V4-03 标记：调用生成的注册方法。参数：<c>method</c>。</summary>
    public const string MigrateMarkerRegisterServices = "cli.migrate.marker.register_services";

    /// <summary>V4-06 标记：在特性里登记生成的操作。</summary>
    public const string MigrateMarkerAddOperations = "cli.migrate.marker.add_operations";

    /// <summary>V4-06 标记：同一资源内动词重复。参数：<c>verb</c>。</summary>
    public const string MigrateMarkerDuplicateVerb = "cli.migrate.marker.duplicate_verb";

    /// <summary>V4-06 标记：方法没法变成操作。参数：<c>name</c>。</summary>
    public const string MigrateMarkerUnsupportedOperation = "cli.migrate.marker.unsupported_operation";

    /// <summary>V4-06 标记：用到了 <c>ApplicationServiceBase</c> 的成员。</summary>
    public const string MigrateMarkerServiceBaseMember = "cli.migrate.marker.service_base_member";

    /// <summary>V4-06 标记：多个权限只取了第一个。</summary>
    public const string MigrateMarkerPermissionSimplified = "cli.migrate.marker.permission_simplified";

    /// <summary>V4-07 标记：<c>ICurrentTenant.Change</c> 没有对应物。</summary>
    public const string MigrateMarkerTenantChange = "cli.migrate.marker.tenant_change";

    /// <summary>V4-08 标记：事件用反射序列化，提醒改用序列化上下文。</summary>
    public const string MigrateMarkerEventJson = "cli.migrate.marker.event_json";

    /// <summary>V4-08 标记：推不出事件类型。参数：<c>name</c>。</summary>
    public const string MigrateMarkerEventTopicUnresolved = "cli.migrate.marker.event_topic_unresolved";

    /// <summary>V4-08 标记：<c>ILocalEventBus.Subscribe</c> 没有对应物。</summary>
    public const string MigrateMarkerEventSubscribe = "cli.migrate.marker.event_subscribe";

    /// <summary>V4-09 标记：装配嵌入的消息目录。参数：<c>domain</c>。</summary>
    public const string MigrateMarkerLocalizationCatalog = "cli.migrate.marker.localization_catalog";

    /// <summary>V4-02 展开属性的文档：创建者标识。</summary>
    public const string MigrateEntityCreatedId = "cli.migrate.entity.created_id";

    /// <summary>V4-02 展开属性的文档：创建人。</summary>
    public const string MigrateEntityCreatedBy = "cli.migrate.entity.created_by";

    /// <summary>V4-02 展开属性的文档：修改者标识。</summary>
    public const string MigrateEntityModifiedId = "cli.migrate.entity.modified_id";

    /// <summary>V4-02 展开属性的文档：修改人。</summary>
    public const string MigrateEntityModifiedBy = "cli.migrate.entity.modified_by";

    /// <summary>V4-02 展开属性的文档：删除者标识。</summary>
    public const string MigrateEntityDeletedId = "cli.migrate.entity.deleted_id";

    /// <summary>V4-02 展开属性的文档：删除人。</summary>
    public const string MigrateEntityDeletedBy = "cli.migrate.entity.deleted_by";

    /// <summary>V4-02 展开成员的文档：领域事件缓冲。</summary>
    public const string MigrateEntityEvents = "cli.migrate.entity.events";

    /// <summary>V4-02 展开成员的文档：记录领域事件。</summary>
    public const string MigrateEntityRaise = "cli.migrate.entity.raise";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(CliMessages));
}
