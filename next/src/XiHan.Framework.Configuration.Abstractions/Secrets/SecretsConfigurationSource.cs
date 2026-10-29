// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Secrets;

/// <summary>
/// 把 <see cref="ISecretStore"/> 接进 <see cref="IConfigurationBuilder"/> 的配置源。
/// </summary>
/// <remarks>
/// 通常经 <see cref="SecretsConfigurationBuilderExtensions.AddXiHanSecrets"/> 使用。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SecretsConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// 创建。
    /// </summary>
    public SecretsConfigurationSource(ISecretStore store, SecretsConfigurationOptions options)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 密钥库。
    /// </summary>
    public ISecretStore Store { get; }

    /// <summary>
    /// 选项。
    /// </summary>
    public SecretsConfigurationOptions Options { get; }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new SecretsConfigurationProvider(Store, Options);
}
