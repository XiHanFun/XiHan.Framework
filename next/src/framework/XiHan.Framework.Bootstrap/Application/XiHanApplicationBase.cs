using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bootstrap.Abstractions.Application;
using XiHan.Framework.Bootstrap.Abstractions.Modularity;
using XiHan.Framework.Bootstrap.Modularity;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Application;

/// <summary>
/// 提供应用基类。
/// </summary>
public abstract class XiHanApplicationBase : IXiHanApplication
{
    private bool _servicesConfigured;
    private bool _initialized;
    private bool _shutdown;

    /// <summary>
    /// 使用指定启动模块和服务集合初始化应用。
    /// </summary>
    /// <param name="startupModuleType">启动模块类型。</param>
    /// <param name="services">服务集合。</param>
    /// <param name="optionsAction">创建选项委托。</param>
    protected XiHanApplicationBase(Type startupModuleType, IServiceCollection services, Action<XiHanApplicationCreationOptions>? optionsAction)
    {
        XiHanModuleHelper.CheckModuleType(startupModuleType);

        StartupModuleType = startupModuleType;
        Services = services ?? throw new ArgumentNullException(nameof(services));

        var options = new XiHanApplicationCreationOptions(services);
        optionsAction?.Invoke(options);

        ApplicationName = options.ApplicationName ?? startupModuleType.Assembly.GetName().Name;
        InstanceId = options.InstanceId;

        Modules = new ModuleLoader().LoadModules(Services, StartupModuleType);

        Services.AddSingleton<IApplicationInfoAccessor>(this);
        Services.AddSingleton(this);
        Services.AddSingleton<IReadOnlyList<IModuleDescriptor>>(Modules);
        Services.AddSingleton<IModuleManager>(_ => new ModuleManager(Modules));

        if (!options.SkipConfigureServices)
        {
            ConfigureServices();
        }
    }

    /// <inheritdoc />
    public Type StartupModuleType { get; }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <inheritdoc />
    public IServiceProvider ServiceProvider { get; protected set; } = default!;

    /// <inheritdoc />
    public string? ApplicationName { get; }

    /// <inheritdoc />
    public string InstanceId { get; }

    /// <summary>
    /// 获取模块集合。
    /// </summary>
    public IReadOnlyList<IModuleDescriptor> Modules { get; }

    /// <inheritdoc />
    public async Task ConfigureServicesAsync()
    {
        if (_servicesConfigured)
        {
            return;
        }

        var context = new ServiceConfigurationContext(Services);

        foreach (var module in Modules)
        {
            if (module.Instance is IPreConfigureServices preConfigureServices)
            {
                await preConfigureServices.PreConfigureServicesAsync(context);
            }
        }

        foreach (var module in Modules)
        {
            await module.Instance.ConfigureServicesAsync(context);
        }

        foreach (var module in Modules)
        {
            if (module.Instance is IPostConfigureServices postConfigureServices)
            {
                await postConfigureServices.PostConfigureServicesAsync(context);
            }
        }

        _servicesConfigured = true;
    }

    /// <summary>
    /// 同步配置服务。
    /// </summary>
    protected virtual void ConfigureServices()
    {
        if (_servicesConfigured)
        {
            return;
        }

        var context = new ServiceConfigurationContext(Services);

        foreach (var module in Modules)
        {
            if (module.Instance is IPreConfigureServices preConfigureServices)
            {
                preConfigureServices.PreConfigureServices(context);
            }
        }

        foreach (var module in Modules)
        {
            module.Instance.ConfigureServices(context);
        }

        foreach (var module in Modules)
        {
            if (module.Instance is IPostConfigureServices postConfigureServices)
            {
                postConfigureServices.PostConfigureServices(context);
            }
        }

        _servicesConfigured = true;
    }

    /// <summary>
    /// 设置服务提供器。
    /// </summary>
    /// <param name="serviceProvider">服务提供器。</param>
    protected void SetServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 异步初始化模块。
    /// </summary>
    protected async Task InitializeModulesAsync()
    {
        EnsureServiceProvider();

        if (_initialized)
        {
            return;
        }

        await ServiceProvider
            .GetRequiredService<IModuleManager>()
            .InitializeModulesAsync(new ApplicationInitializationContext(ServiceProvider));

        _initialized = true;
    }

    /// <summary>
    /// 同步初始化模块。
    /// </summary>
    protected void InitializeModules()
    {
        EnsureServiceProvider();

        if (_initialized)
        {
            return;
        }

        ServiceProvider
            .GetRequiredService<IModuleManager>()
            .InitializeModules(new ApplicationInitializationContext(ServiceProvider));

        _initialized = true;
    }

    /// <inheritdoc />
    public async Task ShutdownAsync()
    {
        if (_shutdown || ServiceProvider is null)
        {
            return;
        }

        await ServiceProvider
            .GetRequiredService<IModuleManager>()
            .ShutdownModulesAsync(new ApplicationShutdownContext(ServiceProvider));

        _shutdown = true;
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        if (_shutdown || ServiceProvider is null)
        {
            return;
        }

        ServiceProvider
            .GetRequiredService<IModuleManager>()
            .ShutdownModules(new ApplicationShutdownContext(ServiceProvider));

        _shutdown = true;
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private void EnsureServiceProvider()
    {
        if (ServiceProvider is null)
        {
            throw new InvalidOperationException("当前应用尚未建立服务提供器。");
        }
    }
}
