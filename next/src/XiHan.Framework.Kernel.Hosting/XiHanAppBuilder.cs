// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// XiHan 应用构建器。支持包装外部 IServiceCollection 和 IConfiguration。
/// </summary>
public sealed class XiHanAppBuilder
{
    private readonly bool _ownsServices;

    /// <summary>
    /// 使用外部服务集合和配置创建构建器。
    /// </summary>
    public XiHanAppBuilder(IServiceCollection externalServices, IConfiguration externalConfiguration)
    {
        Services = externalServices;
        Configuration = externalConfiguration;
        Features = new FeatureCollection();
        _ownsServices = false;
    }

    /// <summary>
    /// 独立创建构建器，自建 ServiceCollection 和 Configuration。
    /// </summary>
    public XiHanAppBuilder(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args ?? [])
            .Build();

        Services = new ServiceCollection();
        Configuration = configuration;
        Features = new FeatureCollection();
        _ownsServices = true;

        Services.AddSingleton<IConfiguration>(configuration);
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
    /// 管道构建器。
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
    /// 构建应用。独立模式创建 ServiceProvider，外部容器模式不创建。
    /// </summary>
    public XiHanApp Build()
    {
        var context = new FeatureConfigurationContext(Services, Configuration);
        foreach (var feature in Features)
            feature.Configure(context);

        Services.AddSingleton(Features);

        if (_ownsServices)
        {
            var provider = Services.BuildServiceProvider();
            return new XiHanApp(provider, Pipeline, Features, ownsProvider: true);
        }

        return new XiHanApp(null!, Pipeline, Features, ownsProvider: false);
    }
}
