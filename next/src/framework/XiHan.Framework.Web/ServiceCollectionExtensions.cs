using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Web;

/// <summary>
/// 提供 Web 层基础服务注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册框架默认 Web 基础服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合，便于链式调用。</returns>
    public static IServiceCollection AddXiHanWebCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
