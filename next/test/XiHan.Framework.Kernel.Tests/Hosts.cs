// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 测试用的宿主工厂：内存配置、最小容器与调用上下文。
/// </summary>
internal static class Hosts
{
    /// <summary>
    /// 用键值对构造内存配置。
    /// </summary>
    public static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>
    /// 用 <see cref="AppBuilder"/> 配置一个新的服务集合并构建容器；返回容器与从中解析出的 <see cref="App"/>。调用方负责释放容器。
    /// </summary>
    /// <param name="configure">对构建器的配置。</param>
    /// <param name="configuration">宿主配置；缺省为空配置。</param>
    public static (ServiceProvider Provider, App App) BuildApp(Action<AppBuilder> configure, IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, configuration ?? Configuration());
        configure(builder);
        builder.Build();
        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<App>());
    }

    /// <summary>
    /// 创建带随机 Trace 的调用上下文。
    /// </summary>
    public static InvocationContext Context(IServiceProvider services, DateTimeOffset? deadline = null, CancellationToken cancellationToken = default)
        => App.CreateContext(services, cancellationToken, deadline: deadline);
}
