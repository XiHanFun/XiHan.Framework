// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 密钥配置源的选项：把配置键映射到密钥名。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SecretsConfigurationOptions
{
    /// <summary>
    /// 创建；映射不能为空。
    /// </summary>
    public SecretsConfigurationOptions(IReadOnlyDictionary<string, SecretName> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        if (mappings.Count == 0)
        {
            throw new ArgumentException("At least one configuration key must be mapped to a secret.", nameof(mappings));
        }

        foreach (var key in mappings.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Configuration keys must not be empty.", nameof(mappings));
            }
        }

        Mappings = mappings;
    }

    /// <summary>
    /// 配置键 → 密钥名。
    /// </summary>
    public IReadOnlyDictionary<string, SecretName> Mappings { get; }

    /// <summary>
    /// 构建配置时读取的超时；默认 10 秒。
    /// </summary>
    public TimeSpan LoadTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The load timeout must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 周期性重读的间隔；默认 <see langword="null"/> 不重读。
    /// </summary>
    public TimeSpan? RefreshInterval
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The refresh interval must be positive.");
    }

    /// <summary>
    /// 首次读取缺失或失败的项是否跳过；默认 <see langword="false"/>：抛出让启动失败。
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>
    /// 读取失败时的回调（每个失败的项一次）。
    /// </summary>
    public Action<Error>? OnError { get; init; }
}
