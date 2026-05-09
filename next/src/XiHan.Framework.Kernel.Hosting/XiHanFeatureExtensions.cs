// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性注册与 Host 集成扩展方法。
/// </summary>
public static class XiHanFeatureExtensions
{
    /// <summary>
    /// 将 IHostedFeature 注册为 IHostedService，每个 feature 创建独立包装器。
    /// </summary>
    public static IServiceCollection AddHostedFeatures(this IServiceCollection services, FeatureCollection features)
    {
        foreach (var feature in features.GetAll())
        {
            if (feature is IHostedFeature hosted)
            {
                services.AddSingleton(hosted);
                var captured = hosted;
                services.AddHostedService(_ => new HostedFeatureWrapper(captured));
            }
        }
        return services;
    }

    /// <summary>
    /// 向 IHostBuilder 注册 XiHan 应用，共享宿主 DI 容器。
    /// </summary>
    public static IHostBuilder UseXiHan(this IHostBuilder hostBuilder, Action<XiHanAppBuilder> configure)
    {
        hostBuilder.ConfigureServices((ctx, services) =>
        {
            var builder = new XiHanAppBuilder(services, ctx.Configuration);
            configure(builder);
            var app = builder.Build();
            services.AddHostedFeatures(app.Features);
            services.AddSingleton(app);
        });
        return hostBuilder;
    }
}

/// <summary>
/// 将 IHostedFeature 包装为 BackgroundService。
/// </summary>
internal sealed class HostedFeatureWrapper : BackgroundService
{
    private readonly IHostedFeature _feature;

    public HostedFeatureWrapper(IHostedFeature feature) => _feature = feature;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _feature.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            => await _feature.StartAsync(stoppingToken);
}
