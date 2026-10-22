// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 密钥配置的构建器扩展。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SecretsConfigurationBuilderExtensions
{
    /// <summary>
    /// 添加密钥配置源：构建时按 <see cref="SecretsConfigurationOptions.Mappings"/> 一次批量读取，可按 <see cref="SecretsConfigurationOptions.RefreshInterval"/> 重读。
    /// </summary>
    public static IConfigurationBuilder AddXiHanSecrets(this IConfigurationBuilder builder, ISecretStore store, SecretsConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new SecretsConfigurationSource(store, options));
    }
}
