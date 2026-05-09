// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// XiHan 应用构建器。提供 Builder 模式的应用构建，支持特性注册和管道配置。
/// </summary>
public sealed class XiHanAppBuilder
{
    public XiHanAppBuilder(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args ?? [])
            .Build();

        Configuration = configuration;
        Services = new ServiceCollection();
        Features = new FeatureCollection();

        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddLogging(builder => builder.AddConsole());
    }

    /// <summary>
    /// 服务集合。
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 应用配置。
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// 已注册的特性集合。
    /// </summary>
    public FeatureCollection Features { get; }

    /// <summary>
    /// 管道构建器。为 null 表示尚未配置管道。
    /// </summary>
    public PipelineBuilder? Pipeline { get; private set; }

    /// <summary>
    /// 注册一个特性。
    /// </summary>
    public XiHanAppBuilder UseFeature<TFeature>() where TFeature : IXiHanFeature, new()
    {
        Features.Add<TFeature>();
        return this;
    }

    /// <summary>
    /// 配置管道。
    /// </summary>
    public XiHanAppBuilder UsePipeline(Action<PipelineBuilder> configure)
    {
        var pipeline = new PipelineBuilder();
        configure(pipeline);
        Pipeline = pipeline;
        return this;
    }

    /// <summary>
    /// 构建并初始化应用。
    /// </summary>
    public XiHanApp Build()
    {
        // 配置每个特性
        var context = new FeatureConfigurationContext(Services, Configuration);
        foreach (var feature in Features)
        {
            feature.Configure(context);
        }

        // 注册特性集合到 DI
        Services.AddSingleton(Features);

        // 构建服务提供器
        var serviceProvider = Services.BuildServiceProvider();

        return new XiHanApp(serviceProvider, Pipeline, Features);
    }
}
