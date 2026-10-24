// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 带注入标记的 4.x 框架基类与接口 → 存期（<c>spec/migration/v4.md</c> §4 V4-03）：应用类沿基类链继承到它们时按该存期注册，
/// 与 4.x 的约定注册器（<c>ConventionalRegistrarBase.GetServiceLifetimeFromClassHierarchy</c>）一致。
/// 表按 <c>framework/src</c> 里 <c>: ITransientDependency / IScopedDependency / ISingletonDependency</c> 的非 sealed 类与接口生成，再加上直接继承它们的框架基类。
/// </summary>
internal static class V4ServiceBases
{
    /// <summary>
    /// 类型简单名 → 存期。
    /// </summary>
    private static readonly Dictionary<string, string> _lifetimes = new(StringComparer.Ordinal)
    {
        // XiHan.Framework.Application
        ["ApplicationServiceBase"] = "Transient",
        ["CrudApplicationServiceBase"] = "Transient",
        ["BatchCrudApplicationServiceBase"] = "Transient",
        // XiHan.Framework.Domain
        ["IDomainService"] = "Transient",
        ["DomainService"] = "Transient",
        // XiHan.Framework.Core
        ["IModuleLifecycleContributor"] = "Transient",
        ["CachedServiceProvider"] = "Scoped",
        ["TransientCachedServiceProvider"] = "Transient",
        ["RootServiceProvider"] = "Singleton",
        ["ExceptionNotifier"] = "Transient",
        ["ModuleManager"] = "Singleton",
        ["DefaultCorrelationIdProvider"] = "Singleton",
        // XiHan.Framework.Caching
        ["CacheAspect"] = "Transient",
        ["CacheInterceptor"] = "Transient",
        // XiHan.Framework.Data
        ["DbInitializer"] = "Scoped",
        // XiHan.Framework.EventBus
        ["ActionEventHandler"] = "Transient",
        ["EventHandlerInvoker"] = "Singleton",
        ["LocalEventBus"] = "Singleton",
        ["LocalDistributedEventBus"] = "Singleton",
        ["KafkaDistributedEventBus"] = "Singleton",
        ["RabbitMQDistributedEventBus"] = "Singleton",
        ["RedisDistributedEventBus"] = "Singleton",
        ["UnitOfWorkEventPublisher"] = "Transient",
        // XiHan.Framework.MultiTenancy
        ["CurrentTenant"] = "Transient",
        ["TenantFeatureChecker"] = "Transient",
        // XiHan.Framework.ObjectMapping
        ["ExtensionPropertyPolicyChecker"] = "Transient",
        // XiHan.Framework.Security / Web.Core
        ["CurrentUser"] = "Transient",
        ["CurrentClient"] = "Transient",
        ["ThreadCurrentPrincipalAccessor"] = "Singleton",
        ["HttpContextCurrentPrincipalAccessor"] = "Scoped",
        // XiHan.Framework.Settings
        ["SettingManager"] = "Scoped",
        ["SettingDefinitionContext"] = "Singleton",
        ["SettingDefinitionManager"] = "Singleton",
        ["SettingValueProvider"] = "Transient",
        ["ConfigurationSettingValueProvider"] = "Transient",
        ["DefaultValueSettingValueProvider"] = "Transient",
        ["GlobalSettingValueProvider"] = "Transient",
        ["UserSettingValueProvider"] = "Transient",
        ["TenantSettingValueProvider"] = "Transient",
        ["NullSettingStore"] = "Singleton",
        // XiHan.Framework.Tasks
        ["IBackgroundWorker"] = "Singleton",
        ["AsyncBackgroundJob"] = "Transient",
        // XiHan.Framework.Threading
        ["AsyncLocalAmbientDataContext"] = "Singleton",
        // XiHan.Framework.Uow
        ["UnitOfWork"] = "Transient",
        ["UnitOfWorkManager"] = "Singleton",
        ["UnitOfWorkInterceptor"] = "Transient",
        ["AmbientUnitOfWork"] = "Singleton",
        ["NullUnitOfWorkEventPublisher"] = "Singleton",
        ["NullUnitOfWorkTransactionBehaviourProvider"] = "Singleton",
    };

    /// <summary>
    /// 查 4.x 框架基类或接口的存期；不在表里返回 <see langword="false"/>。
    /// </summary>
    public static bool TryGetLifetime(string name, out string lifetime) => _lifetimes.TryGetValue(name, out lifetime!);
}
