// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 把 <see cref="IRemoteConfigurationClient"/> 接进 <see cref="IConfigurationBuilder"/> 的配置源。
/// </summary>
/// <remarks>
/// 通常经 <see cref="RemoteConfigurationBuilderExtensions.AddXiHanRemoteConfiguration"/> 使用。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RemoteConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// 创建。
    /// </summary>
    public RemoteConfigurationSource(IRemoteConfigurationClient client, RemoteConfigurationOptions options)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 客户端。
    /// </summary>
    public IRemoteConfigurationClient Client { get; }

    /// <summary>
    /// 选项。
    /// </summary>
    public RemoteConfigurationOptions Options { get; }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new RemoteConfigurationProvider(Client, Options);
}
