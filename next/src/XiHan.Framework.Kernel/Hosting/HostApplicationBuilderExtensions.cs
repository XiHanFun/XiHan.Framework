// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// <see cref="IHostApplicationBuilder"/> 的接入点。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// 在宿主构建器上装配 XiHan 应用：共享宿主的服务集合与配置，<see cref="App"/> 随宿主启动与停止。
    /// </summary>
    public static TBuilder AddXiHan<TBuilder>(this TBuilder builder, Action<AppBuilder> configure)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var appBuilder = new AppBuilder(builder.Services, builder.Configuration);
        configure(appBuilder);
        appBuilder.Build();
        return builder;
    }
}
