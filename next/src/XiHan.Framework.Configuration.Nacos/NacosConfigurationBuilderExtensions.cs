// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// 把 Nacos 接进 <see cref="IConfigurationBuilder"/>：按选项自建客户端，交给 <see cref="RemoteConfigurationBuilderExtensions.AddXiHanRemoteConfiguration"/>（首次同步加载、按间隔轮询重载）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class NacosConfigurationBuilderExtensions
{
    /// <summary>
    /// 加入 Nacos 配置源：<paramref name="remote"/> 的命名空间即 Nacos 的 <c>dataId</c>。
    /// </summary>
    public static IConfigurationBuilder AddXiHanNacos(this IConfigurationBuilder builder, NacosOptions options, RemoteConfigurationOptions remote)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(remote);
        return builder.AddXiHanRemoteConfiguration(NacosConfigurationClient.Create(options), remote);
    }
}
