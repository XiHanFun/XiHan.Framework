using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示使用内部服务提供器的应用抽象。
/// </summary>
public interface IXiHanApplicationWithInternalServiceProvider : IXiHanApplication
{
    /// <summary>
    /// 获取内部创建的服务作用域。
    /// </summary>
    IServiceScope? ServiceScope { get; }

    /// <summary>
    /// 创建服务提供器。
    /// </summary>
    /// <returns>服务提供器。</returns>
    IServiceProvider CreateServiceProvider();

    /// <summary>
    /// 创建服务提供器并完成初始化。
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 创建服务提供器并完成初始化。
    /// </summary>
    void Initialize();
}
