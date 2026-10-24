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
    /// 4.x 框架模块 → 它的 <c>ConfigureServices</c> 注册的服务（按 <c>framework/src</c> 各模块的扩展方法整理）。删除模块时这些注册随之丢失，
    /// 承接代码仍依赖的要在特性的 <c>Configure</c> 里补回；表里没有的模块（<c>XiHanUowModule</c>、<c>XiHanCastleModule</c> 等）没有应用会消费的注册。
    /// </summary>
    private static readonly Dictionary<string, V4ModuleRegistration[]> _registrations = new(StringComparer.Ordinal)
    {
        ["XiHanWebCoreModule"] = [new("AddXiHanWebCore", true, ["IHttpContextAccessor", "XiHanClientInfoOptions", "IIpRegionResolver", "IClientInfoProvider", "ICurrentPrincipalAccessor", "IClaimsTransformation"])],
        ["XiHanWebApiModule"] =
        [
            new("AddXiHanWebApi", true, ["IRequestContextAccessor", "ITraceIdProvider", "IOpenApiSecurityClientStore", "ISessionStateGate", "XiHanSessionStateMiddleware", "XiHanRequestAccessMiddleware", "AddCors", "AddXiHanWebApiAuth", "AddXiHanWebApiLogging", "AddXiHanWebApiMvc"]),
            new("AddXiHanRateLimiting", true, ["XiHanRateLimitingOptions"]),
            new("AddXiHanCircuitBreaking", true, ["XiHanCircuitBreakingOptions"]),
            new(null, false, ["XiHanForwardedHeadersOptions"]),
        ],
        ["XiHanWebRealTimeModule"] = [new("AddXiHanSignalRWithJson", true, ["AddSignalR", "IConnectionManager", "IUserIdProvider", "IXiHanUserIdProvider"])],
        ["XiHanDistributedIdsModule"] = [new("AddXiHanDistributedIds", true, ["SnowflakeIdOptions", "SequentialGuidOptions", "NanoIdOptions", "SqidsOptions", "IdGeneratorFactory.CreateSnowflakeIdGenerator", "IdGeneratorFactory.CreateSequentialGuidGenerator"])],
        ["XiHanUpgradeModule"] = [new("AddXiHanUpgrade", true, ["XiHanUpgradeOptions", "IUpgradeScriptProvider", "IUpgradeVersionStore", "IUpgradeLockProvider", "IUpgradeTenantProvider", "IUpgradeMigrationExecutor", "IUpgradeStatusService", "IUpgradeEngine", "IUpgradeCoordinator", "IUpgradeMaintenanceModeManager", "IUpgradeFileUpdater"])],
        ["XiHanCachingModule"] = [new("AddXiHanCaching", true, ["IMemoryCache", "IDistributedCache (AddDistributedMemoryCache / AddStackExchangeRedisCache by XiHan:Caching:Redis:IsEnabled)", "XiHanRedisCacheOptions", "IDistributedCacheKeyNormalizer", "IDistributedCacheSerializer", "IDistributedCache<>", "IDistributedCache<,>", "HybridCache", "IHybridCache<>", "IHybridCache<,>", "IDistributedLock", "IRedisDelayQueue<> (DefaultDelayQueue<> / RedisDelayQueue<>)"])],
        ["XiHanSecurityModule"] = [new("AddXiHanSecurityServices", true, ["PasswordHasherOptions", "PasswordPolicyOptions", "IPasswordHasher", "IPasswordPolicyService", "IPasswordHistoryStore"])],
        ["XiHanAuthenticationModule"] = [new("AddXiHanAuthentication", true, ["IAuthenticationService", "IUserStore", "IJwtTokenService", "IOneTimeCodeService", "IOtpService", "IRefreshTokenStore", "AddXiHanOAuth"])],
        ["XiHanAuthorizationModule"] = [new("AddXiHanAuthorization", true, ["IAuthorizationService", "IPermissionChecker", "IPermissionStore", "IPolicyEvaluator", "IPolicyStore", "IRoleStore", "IAbacEvaluator", "IAbacAttributeCollector", "IAuthorizationPolicyProvider", "IConditionAttributeRegistry"])],
        ["XiHanMultiTenancyModule"] = [new("AddXiHanMultiTenancy", true, ["ICurrentTenantAccessor", "ITenantStore", "ITenantFeatureChecker"])],
        ["XiHanLocalizationModule"] = [new("AddXiHanLocalization", true, ["AddLocalization", "XiHanLocalizationOptions", "IEnumLocalizationService", "JsonLocalizationResourceStore", "ResourceManagerStringLocalizerFactory"])],
        ["XiHanDataModule"] = [new("AddXiHanDataSqlSugar", true, ["ISqlSugarClientResolver", "ISqlSugarTenantConnectionResolver", "IModuleDataSourceConnectionResolver", "IDbInitializer", "IDatabaseMetadataProvider", "IDataSeederSelector", "IDbEntityTypeProvider", "IDynamicConnectionRegistrar", "IEntityModuleDataSourceResolver", "SqlSugarSlaveHealthCheckService"])],
        ["XiHanEventBusModule"] = [new("AddXiHanEventBus", true, ["ILocalEventBus", "IDistributedEventBus", "IEventHandlerInvoker", "IUnitOfWorkEventPublisher"])],
        ["XiHanTasksModule"] =
        [
            new("AddXiHanBackgroundJobs", true, ["IBackgroundJobManager", "IBackgroundJobExecuter", "IBackgroundJobStore", "IBackgroundJobSerializer", "BackgroundJobWorker"]),
            new("AddXiHanTasks", true, ["XiHanJobOptions", "IJobScheduler", "IJobStore", "IJobExecutor", "IJobLockProvider", "IJobEventPublisher", "JobMetricsProvider", "JobHostedService"]),
        ],
        ["XiHanAuditingModule"] = [new("AddXiHanAuditing", true, ["IAccessLogPipeline", "IApiLogPipeline", "IExceptionLogPipeline", "ILoginLogPipeline", "IOperationLogPipeline", "IAccessLogWriter", "*LogQueueWorker"])],
        ["XiHanObjectStorageModule"] = [new("AddXiHanObjectStorage", true, ["IFileStorageProviderManager", "IFileStorageRouter"])],
        ["XiHanObservabilityModule"] = [new("AddXiHanObservability", true, ["AddOpenTelemetry", "AddHealthChecks", "IDiagnosticsService", "IMetricsCollector", "IPerformanceMonitor"])],
        ["XiHanLoggingModule"] = [new("AddXiHanLogging", true, ["AddXiHanSerilog", "IXiHanLoggerFactory", "IXiHanLogger", "IStructuredLogger", "IPerformanceLogger", "ILogContext"])],
        ["XiHanSettingsModule"] = [new("AddXiHanSettings", true, ["ISettingManager", "ISettingDefinitionManager", "ISettingStore", "ISettingValueProvider"])],
        ["XiHanHttpModule"] = [new("AddXiHanHttpModule", true, ["IProxyValidator", "IProxyPoolManager", "IAdvancedHttpService"])],
        ["XiHanThreadingModule"] = [new("AddXiHanThreading", false, ["ICancellationTokenProvider", "IAmbientDataContext"])],
        ["XiHanTimingModule"] = [new("AddXiHanTiming", false, ["XiHanClockOptions", "IClock", "ITimezoneProvider", "ICurrentTimezoneProvider"])],
        ["XiHanObjectMappingModule"] = [new("AddXiHanMapster", false, ["IMapper"])],
        ["XiHanTemplatingModule"] = [new("AddXiHanTemplating", false, ["ITemplateEngineRegistry", "ITemplateService", "ITemplateContextAccessor", "ITemplateContextFactory", "ITemplateInheritanceManager", "ITemplatePartialManager"])],
        ["XiHanMessagingModule"] = [new("AddXiHanMessaging", false, ["IMessageDispatcher"])],
        ["XiHanBotModule"] = [new("AddXiHanBot", false, ["XiHanBotOptions", "BotProviderManager", "BotDispatcher"])],
        ["XiHanAIModule"] = [new("AddXiHanAI", false, ["XiHanAiOptions", "AiGuardrailOptions", "IAiChatClientResolver", "IAiEmbeddingGeneratorResolver", "IAiProviderConfigStore"]), new("AddXiHanRAG", false, [])],
        ["XiHanVirtualFileSystemModule"] = [new("AddXiHanVirtualFileSystem", true, ["VirtualFileSystemOptions", "IVirtualFileSystem", "IFileVersioningService"])],
        ["XiHanWorkflowModule"] = [new("AddXiHanWorkflow", true, ["IWorkflowEngine", "IWorkflowDefinitionManager", "IWorkflowDefinitionStore", "IWorkflowInstanceStore", "IWorkflowBookmarkStore", "IWorkflowActivityRegistry", "IWorkflowExpressionEvaluator", "IWorkflowEventPublisher", "IWorkflowAssigneeResolver", "WorkflowTimerWorker"])],
        ["XiHanWebGatewayModule"] = [new("AddGrayRouting", false, ["IGrayRuleEngine", "IGrayMatcher"])],
        ["XiHanTrafficModule"] = [new("AddGrayRouting", false, ["IGrayRuleEngine", "IGrayMatcher"])],
        ["XiHanSearchEnginesModule"] = [new(null, false, ["ISearchEngine (DefaultSearchEngine)"])],
    };

    /// <summary>
    /// 4.x 框架模块在 <c>ConfigureServices</c> 里做的注册；没有应用会消费的注册时为空。<c>XiHanBot&lt;平台&gt;Module</c> 对应 <c>AddXiHanBot&lt;平台&gt;()</c>。
    /// </summary>
    public static IReadOnlyList<V4ModuleRegistration> Registrations(string module)
    {
        if (_registrations.TryGetValue(module, out var registrations))
        {
            return registrations;
        }

        if (module.StartsWith("XiHanBot", StringComparison.Ordinal) && module.EndsWith("Module", StringComparison.Ordinal) && module.Length > "XiHanBotModule".Length)
        {
            var platform = module["XiHanBot".Length..^"Module".Length];
            return [new V4ModuleRegistration("AddXiHanBot" + platform, false, [])];
        }

        return [];
    }

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
