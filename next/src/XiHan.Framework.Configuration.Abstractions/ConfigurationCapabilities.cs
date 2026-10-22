// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 配置子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConfigurationCapabilities
{
    /// <summary>远程配置：按命名空间取整份带版本的文档，<see cref="IRemoteConfigurationClient"/>。</summary>
    public const string Remote = "configuration.remote";

    /// <summary>密钥：按名字批量读取，<see cref="ISecretStore"/>。</summary>
    public const string Secrets = "configuration.secrets";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}
