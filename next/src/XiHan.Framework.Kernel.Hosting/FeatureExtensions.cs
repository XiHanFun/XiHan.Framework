// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性相关的扩展方法。
/// </summary>
public static class FeatureExtensions
{
    /// <summary>
    /// 注册一个特性到服务集合中。
    /// </summary>
    public static IServiceCollection AddXiHanFeature<TFeature>(this IServiceCollection services)
        where TFeature : class, IXiHanFeature
        => services.AddSingleton<IXiHanFeature, TFeature>();

    /// <summary>
    /// 将特性集合中所有 IHostedFeature 注册为 HostedService。
    /// </summary>
    public static IServiceCollection AddHostedFeatures(this IServiceCollection services, FeatureCollection features)
    {
        foreach (var feature in features.GetAll())
        {
            if (feature is IHostedFeature hostedFeature)
            {
                services.AddSingleton(hostedFeature);
                services.AddHostedService(sp => new HostedFeatureWrapper(sp.GetRequiredService<IHostedFeature>()));
            }
        }
        return services;
    }

    /// <summary>
    /// 向 Microsoft.Extensions.Hosting 注册 XiHan 应用。
    /// </summary>
    public static IHostBuilder UseXiHan(this IHostBuilder hostBuilder, Action<XiHanAppBuilder> configure)
    {
        return hostBuilder.ConfigureServices((_, services) =>
        {
            var builder = new XiHanAppBuilder(null);
            configure(builder);
            var app = builder.Build();

            // 将所有特性注入到服务集合
            foreach (var feature in app.Features)
            {
                if (feature is IHostedFeature hosted)
                {
                    services.AddSingleton(hosted);
                    services.AddHostedService<HostedFeatureWrapper>();
                }
            }

            services.AddSingleton(app);
        });
    }
}

/// <summary>
/// 将 IHostedFeature 包装为 .NET BackgroundService。
/// </summary>
internal sealed class HostedFeatureWrapper : BackgroundService
{
    private readonly IHostedFeature _feature;

    public HostedFeatureWrapper(IHostedFeature feature) => _feature = feature;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await _feature.StartAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _feature.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
