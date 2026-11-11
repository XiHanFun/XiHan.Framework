// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 远程配置的构建器扩展
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class RemoteConfigurationBuilderExtensions
{
    /// <summary>
    /// 添加远程配置源
    /// </summary>
    /// <remarks>
    /// 构建时同步加载一次（受 <see cref="RemoteConfigurationOptions.LoadTimeout"/> 约束），之后按 <see cref="RemoteConfigurationOptions.PollInterval"/> 轮询并在变化时触发重载。<paramref name="client"/> 由调用方持有，配置根释放时不释放它
    /// </remarks>
    public static IConfigurationBuilder AddXiHanRemoteConfiguration(this IConfigurationBuilder builder, IRemoteConfigurationClient client, RemoteConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new RemoteConfigurationSource(client, options));
    }

    /// <summary>
    /// 添加远程配置源，客户端由它按工厂创建并负责释放
    /// </summary>
    /// <remarks>
    /// 加载与轮询同 <see cref="AddXiHanRemoteConfiguration(IConfigurationBuilder, IRemoteConfigurationClient, RemoteConfigurationOptions)"/>。每次构建 Provider（构建配置根，或 <c>ConfigurationManager</c> 重建全部源）调用 <paramref name="clientFactory"/> 新建一个客户端，归这个 Provider 所有：配置根释放、Provider 被重建替换时一并释放（客户端实现 <see cref="IDisposable"/> 时）。用于配置构建期自建、没有别人持有的客户端
    /// </remarks>
    public static IConfigurationBuilder AddXiHanRemoteConfiguration(this IConfigurationBuilder builder, Func<IRemoteConfigurationClient> clientFactory, RemoteConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new OwnedRemoteConfigurationSource(clientFactory, options));
    }
}
