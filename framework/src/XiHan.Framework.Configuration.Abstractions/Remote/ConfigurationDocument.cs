// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Frozen;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 远程配置的一份文档：版本与已展平的键值（键是 <c>IConfiguration</c> 的 <c>A:B:C</c> 形式）
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConfigurationDocument
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// 版本不能为空，键不能为空，键按配置的规则不区分大小写、不能重复。键值复制成只读副本，调用方之后改原字典不影响文档（ADR-0161）
    /// </remarks>
    public ConfigurationDocument(string version, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(values);
        var copy = new Dictionary<string, string?>(values.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Configuration keys must not be empty.", nameof(values));
            }

            // 配置键不区分大小写：只差大小写的两个键在配置里是同一个键，留哪个都是猜
            if (!copy.TryAdd(key, value))
            {
                throw new ArgumentException($"Configuration key '{key}' appears more than once when compared without regard to case.", nameof(values));
            }
        }

        Version = version;
        Values = copy.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 版本：ETag、修订号或内容哈希，每次变化都不同
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 键值
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values { get; }

    /// <summary>
    /// 指定版本的空文档
    /// </summary>
    public static ConfigurationDocument Empty(string version) => new(version, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override string ToString() => $"version {Version}, {Values.Count} keys";
}
