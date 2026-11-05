// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 按工厂为每个 Provider 新建客户端的远程配置源
/// </summary>
/// <remarks>
/// 经 <see cref="RemoteConfigurationBuilderExtensions.AddXiHanRemoteConfiguration(IConfigurationBuilder, Func{IRemoteConfigurationClient}, RemoteConfigurationOptions)"/> 使用。客户端归 Provider 所有、随它释放；每次构建各建一个，同一构建器构建多次或 <c>ConfigurationManager</c> 重建全部源时，不会有两个 Provider 共用、也不会有 Provider 拿到已释放的客户端
/// </remarks>
internal sealed class OwnedRemoteConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// 客户端工厂
    /// </summary>
    private readonly Func<IRemoteConfigurationClient> _clientFactory;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly RemoteConfigurationOptions _options;

    /// <summary>
    /// 创建
    /// </summary>
    public OwnedRemoteConfigurationSource(Func<IRemoteConfigurationClient> clientFactory, RemoteConfigurationOptions options)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var client = _clientFactory() ?? throw new InvalidOperationException("The remote configuration client factory returned null.");
        return new RemoteConfigurationProvider(client, _options, ownsClient: true);
    }
}
