using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Authorization;

/// <summary>
/// 提供授权服务注册扩展方法。
/// </summary>
public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// 注册框架默认授权服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddXiHanAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
