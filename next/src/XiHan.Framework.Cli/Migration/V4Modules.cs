// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 4.x 框架模块 → 5.0 能力的映射（<c>spec/migration/v4.md</c> §4.1），以及能力 ID 在代码里的常量引用。
/// </summary>
internal static class V4Modules
{
    /// <summary>
    /// 每份清单都要求的宿主能力。
    /// </summary>
    public const string Hosting = "kernel.hosting";

    /// <summary>
    /// 4.x 模块类名 → 能力 ID；为空表示 5.0 没有对应能力（删除并记报告）。表里没有的名字是应用自己的模块。
    /// </summary>
    private static readonly Dictionary<string, string?> _modules = new(StringComparer.Ordinal)
    {
        ["XiHanDataModule"] = "persistence.relational",
        ["XiHanUowModule"] = "persistence.relational",
        ["XiHanDomainModule"] = "persistence.relational",
        ["XiHanDistributedIdsModule"] = "persistence.relational",
        ["XiHanCachingModule"] = "caching.distributed",
        ["XiHanEventBusModule"] = "messaging.queue",
        ["XiHanEventBusAbstractionsModule"] = "messaging.queue",
        ["XiHanKafkaEventBusModule"] = "messaging.queue",
        ["XiHanRabbitMQEventBusModule"] = "messaging.queue",
        ["XiHanRedisEventBusModule"] = "messaging.queue",
        ["XiHanMessagingModule"] = "messaging.queue",
        ["XiHanAuthenticationModule"] = "security.authentication",
        ["XiHanAuthorizationModule"] = "security.authorization",
        ["XiHanSecurityModule"] = "security.authorization",
        ["XiHanMultiTenancyModule"] = "multitenancy.context",
        ["XiHanMultiTenancyAbstractionsModule"] = "multitenancy.context",
        ["XiHanLocalizationModule"] = "localization.resources",
        ["XiHanLocalizationAbstractionsModule"] = "localization.resources",
        ["XiHanObjectStorageModule"] = "objectstorage.blob",
        ["XiHanTasksModule"] = "scheduling.jobs",
        ["XiHanAuditingModule"] = "auditing.trail",
        ["XiHanObservabilityModule"] = "observability.export",
        ["XiHanWebCoreModule"] = "protocols.http",
        ["XiHanWebApiModule"] = "protocols.http",
        ["XiHanWebDocsModule"] = "protocols.http",
        ["XiHanWebGrpcModule"] = "protocols.grpc",
        ["XiHanWebMcpModule"] = "protocols.mcp",
        ["XiHanCoreModule"] = null,
        ["XiHanApplicationModule"] = null,
        ["XiHanApplicationContractsModule"] = null,
        ["XiHanDomainSharedModule"] = null,
        ["XiHanCastleModule"] = null,
        ["XiHanHttpModule"] = null,
        ["XiHanSerializationModule"] = null,
        ["XiHanThreadingModule"] = null,
        ["XiHanTimingModule"] = null,
        ["XiHanValidationModule"] = null,
        ["XiHanValidationAbstractionsModule"] = null,
        ["XiHanObjectMappingModule"] = null,
        ["XiHanLoggingModule"] = null,
        ["XiHanSettingsModule"] = null,
        ["XiHanScriptModule"] = null,
        ["XiHanTemplatingModule"] = null,
        ["XiHanSearchEnginesModule"] = null,
        ["XiHanVirtualFileSystemModule"] = null,
        ["XiHanUpgradeModule"] = null,
        ["XiHanAIModule"] = null,
        ["XiHanBotModule"] = null,
        ["XiHanWebRealTimeModule"] = null,
        ["XiHanWebGatewayModule"] = null,
        ["XiHanDevToolsModule"] = null,
        ["XiHanTrafficModule"] = null,
        ["XiHanWorkflowModule"] = null,
        ["XiHanWorkflowAbstractionsModule"] = null,
    };

    /// <summary>
    /// 能力 ID → 常量类、成员与命名空间。
    /// </summary>
    private static readonly Dictionary<string, (string Class, string Member, string Namespace)> _capabilities = new(StringComparer.Ordinal)
    {
        [Hosting] = ("KernelCapabilities", "Hosting", "XiHan.Framework.Kernel"),
        ["persistence.relational"] = ("PersistenceCapabilities", "Relational", "XiHan.Framework.Persistence.Abstractions"),
        ["caching.distributed"] = ("CachingCapabilities", "Distributed", "XiHan.Framework.Caching.Abstractions"),
        ["messaging.queue"] = ("MessagingCapabilities", "Queue", "XiHan.Framework.Messaging.Abstractions"),
        ["security.authentication"] = ("SecurityCapabilities", "Authentication", "XiHan.Framework.Security.Abstractions"),
        ["security.authorization"] = ("SecurityCapabilities", "Authorization", "XiHan.Framework.Security.Abstractions"),
        ["multitenancy.context"] = ("MultiTenancyCapabilities", "Context", "XiHan.Framework.MultiTenancy.Abstractions"),
        ["localization.resources"] = ("LocalizationCapabilities", "Resources", "XiHan.Framework.Localization.Abstractions"),
        ["objectstorage.blob"] = ("ObjectStorageCapabilities", "Blob", "XiHan.Framework.ObjectStorage.Abstractions"),
        ["scheduling.jobs"] = ("SchedulingCapabilities", "Jobs", "XiHan.Framework.Scheduling.Abstractions"),
        ["auditing.trail"] = ("AuditingCapabilities", "Trail", "XiHan.Framework.Auditing.Abstractions"),
        ["observability.export"] = ("ObservabilityCapabilities", "Export", "XiHan.Framework.Observability.OpenTelemetry"),
        ["protocols.http"] = ("HttpProtocolCapabilities", "Http", "XiHan.Framework.Protocols.Http"),
        ["protocols.grpc"] = ("GrpcProtocolCapabilities", "Grpc", "XiHan.Framework.Protocols.Grpc"),
        ["protocols.mcp"] = ("McpProtocolCapabilities", "Mcp", "XiHan.Framework.Protocols.Mcp"),
    };

    /// <summary>
    /// 查 4.x 框架模块：返回 <see langword="true"/> 时 <paramref name="capability"/> 为能力 ID 或空（无对应能力）；返回 <see langword="false"/> 表示不是框架模块。
    /// </summary>
    public static bool TryGetCapability(string module, out string? capability)
    {
        if (_modules.TryGetValue(module, out capability))
        {
            return true;
        }

        // XiHanBot*Module 各子模块一并视为删除
        if (module.StartsWith("XiHanBot", StringComparison.Ordinal) && module.EndsWith("Module", StringComparison.Ordinal))
        {
            capability = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 能力 ID 在代码里的引用。
    /// </summary>
    public static (string Class, string Member, string Namespace) Reference(string capability) => _capabilities[capability];
}
