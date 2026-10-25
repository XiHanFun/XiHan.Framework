// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 远程配置的构建器扩展。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class RemoteConfigurationBuilderExtensions
{
    /// <summary>
    /// 添加远程配置源：构建时同步加载一次（受 <see cref="RemoteConfigurationOptions.LoadTimeout"/> 约束），之后按 <see cref="RemoteConfigurationOptions.PollInterval"/> 轮询并在变化时触发重载。
    /// </summary>
    public static IConfigurationBuilder AddXiHanRemoteConfiguration(this IConfigurationBuilder builder, IRemoteConfigurationClient client, RemoteConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new RemoteConfigurationSource(client, options));
    }
}
