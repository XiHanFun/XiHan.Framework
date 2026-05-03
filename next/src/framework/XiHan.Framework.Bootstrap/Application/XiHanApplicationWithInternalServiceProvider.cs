using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bootstrap.Abstractions.Application;

namespace XiHan.Framework.Bootstrap.Application;

/// <summary>
/// 提供内部服务提供器模式的应用实现。
/// </summary>
public sealed class XiHanApplicationWithInternalServiceProvider : XiHanApplicationBase, IXiHanApplicationWithInternalServiceProvider
{
    /// <summary>
    /// 使用指定启动模块类型初始化应用。
    /// </summary>
    /// <param name="startupModuleType">启动模块类型。</param>
    /// <param name="optionsAction">创建选项委托。</param>
    public XiHanApplicationWithInternalServiceProvider(Type startupModuleType, Action<XiHanApplicationCreationOptions>? optionsAction = null)
        : base(startupModuleType, new ServiceCollection(), optionsAction)
    {
        Services.AddSingleton<IXiHanApplicationWithInternalServiceProvider>(this);
    }

    /// <inheritdoc />
    public IServiceScope? ServiceScope { get; private set; }

    /// <inheritdoc />
    public IServiceProvider CreateServiceProvider()
    {
        if (ServiceProvider is not null)
        {
            return ServiceProvider;
        }

        ServiceScope = Services.BuildServiceProvider().CreateScope();
        SetServiceProvider(ServiceScope.ServiceProvider);
        return ServiceProvider!;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        CreateServiceProvider();
        await InitializeModulesAsync();
    }

    /// <inheritdoc />
    public void Initialize()
    {
        CreateServiceProvider();
        InitializeModules();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        ServiceScope?.Dispose();
        base.Dispose();
    }
}
