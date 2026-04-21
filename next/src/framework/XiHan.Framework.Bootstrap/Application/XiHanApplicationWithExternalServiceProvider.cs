using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bootstrap.Abstractions.Application;

namespace XiHan.Framework.Bootstrap.Application;

/// <summary>
/// 提供外部服务提供器模式的应用实现。
/// </summary>
public sealed class XiHanApplicationWithExternalServiceProvider : XiHanApplicationBase, IXiHanApplicationWithExternalServiceProvider
{
    /// <summary>
    /// 使用指定启动模块和服务集合初始化应用。
    /// </summary>
    /// <param name="startupModuleType">启动模块类型。</param>
    /// <param name="services">服务集合。</param>
    /// <param name="optionsAction">创建选项委托。</param>
    public XiHanApplicationWithExternalServiceProvider(
        Type startupModuleType,
        IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        : base(startupModuleType, services, optionsAction)
    {
        services.AddSingleton<IXiHanApplicationWithExternalServiceProvider>(this);
    }

    /// <inheritdoc />
    void IXiHanApplicationWithExternalServiceProvider.SetServiceProvider(IServiceProvider serviceProvider)
    {
        if (ServiceProvider is not null && ReferenceEquals(ServiceProvider, serviceProvider))
        {
            return;
        }

        if (ServiceProvider is not null && !ReferenceEquals(ServiceProvider, serviceProvider))
        {
            throw new InvalidOperationException("当前应用已经绑定了另一个服务提供器实例。");
        }

        SetServiceProvider(serviceProvider);
    }

    /// <inheritdoc />
    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        ((IXiHanApplicationWithExternalServiceProvider)this).SetServiceProvider(serviceProvider);
        await InitializeModulesAsync();
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider serviceProvider)
    {
        ((IXiHanApplicationWithExternalServiceProvider)this).SetServiceProvider(serviceProvider);
        InitializeModules();
    }
}
