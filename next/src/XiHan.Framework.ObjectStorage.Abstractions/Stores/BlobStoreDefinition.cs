// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 一个命名存储实例的定义：名字（<c>^[a-z0-9][a-z0-9-]{0,63}$</c>）、Provider 名（如 <c>filesystem</c>、<c>s3</c>，不区分大小写）与 Provider 的设置。
/// 设置的键与该 Provider 配置节下的键相同（如 S3 的 <c>ServiceUrl</c>、<c>AccessKey</c>，文件系统的 <c>RootPath</c>），不区分大小写；可以来自配置，也可以来自应用自己的存储（运行时登记）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record BlobStoreDefinition
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="name">实例名。</param>
    /// <param name="provider">Provider 名。</param>
    /// <param name="settings">Provider 的设置。</param>
    public BlobStoreDefinition(string name, string provider, IReadOnlyDictionary<string, string> settings)
    {
        Name = IsValidName(name) ? name : throw new ArgumentException($"Blob store name '{name}' is invalid: it must be 1 to 64 lowercase letters, digits or hyphens, starting with a letter or digit.", nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(settings);
        Provider = provider;
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in settings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(settings));
            copy[key] = value ?? throw new ArgumentException($"Setting '{key}' must not be null.", nameof(settings));
        }

        Settings = copy;
    }

    /// <summary>
    /// 实例名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Provider 名。
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Provider 的设置（键不区分大小写）。
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings { get; }

    /// <summary>
    /// 实例名是否合法。
    /// </summary>
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>
    /// 是否与另一个定义描述同一个实例：Provider 名（不区分大小写）与设置（键不区分大小写、值区分）完全相同。
    /// </summary>
    public bool Describes(BlobStoreDefinition other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Provider, other.Provider, StringComparison.OrdinalIgnoreCase)
            && Settings.Count == other.Settings.Count
            && Settings.All(pair => other.Settings.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    /// <summary>
    /// 实例名的匹配。
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex NamePattern();
}
