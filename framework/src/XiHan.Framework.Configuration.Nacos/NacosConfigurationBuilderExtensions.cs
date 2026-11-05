// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// 把 Nacos 接进 <see cref="IConfigurationBuilder"/>
/// </summary>
/// <remarks>
/// 按选项自建客户端，交给 <see cref="RemoteConfigurationBuilderExtensions.AddXiHanRemoteConfiguration(IConfigurationBuilder, Func{IRemoteConfigurationClient}, RemoteConfigurationOptions)"/>（首次同步加载、按间隔轮询重载）；客户端归配置源的 Provider 所有，随配置根释放
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class NacosConfigurationBuilderExtensions
{
    /// <summary>
    /// 加入 Nacos 配置源：<paramref name="remote"/> 的命名空间即 Nacos 的 <c>dataId</c>
    /// </summary>
    /// <remarks>
    /// 每个 Provider 自建一个 <see cref="NacosConfigurationClient"/>（自带 <see cref="HttpClient"/>），配置根释放时一并释放
    /// </remarks>
    public static IConfigurationBuilder AddXiHanNacos(this IConfigurationBuilder builder, NacosOptions options, RemoteConfigurationOptions remote)
        => AddXiHanNacosCore(builder, options, remote, handlerFactory: null);

    /// <summary>
    /// 加入 Nacos 配置源，HTTP 处理器由测试替换
    /// </summary>
    /// <remarks>
    /// <paramref name="handlerFactory"/> 为空时用缺省处理器；给出时每个自建的客户端各取一个，随客户端释放
    /// </remarks>
    internal static IConfigurationBuilder AddXiHanNacosCore(this IConfigurationBuilder builder, NacosOptions options, RemoteConfigurationOptions remote, Func<HttpMessageHandler>? handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(remote);
        return builder.AddXiHanRemoteConfiguration(() => NacosConfigurationClient.Create(options, clock: null, handlerFactory?.Invoke()), remote);
    }
}
