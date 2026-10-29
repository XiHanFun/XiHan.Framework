// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 写入选项：内容类型、自定义元数据（名 <c>^[a-z0-9][a-z0-9-]{0,63}$</c>）、是否允许覆盖。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record BlobPutOptions
{
    /// <summary>
    /// 默认内容类型。
    /// </summary>
    public const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// 默认选项：二进制、无元数据、允许覆盖。
    /// </summary>
    public static BlobPutOptions Default { get; } = new();

    /// <summary>
    /// 内容类型。
    /// </summary>
    public string ContentType
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The content type must not be empty.", nameof(value));
    } = DefaultContentType;

    /// <summary>
    /// 自定义元数据。
    /// </summary>
    /// <remarks>
    /// 名须满足 <see cref="IsValidMetadataName"/>。
    /// </remarks>
    public IReadOnlyDictionary<string, string> Metadata
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var (name, text) in value)
            {
                if (!IsValidMetadataName(name))
                {
                    throw new ArgumentException($"Metadata name '{name}' is invalid: it must be 1 to 64 lowercase letters, digits or hyphens, starting with a letter or digit.", nameof(value));
                }

                ArgumentNullException.ThrowIfNull(text, nameof(value));
            }

            field = value;
        }
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 已存在时是否覆盖。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="false"/> 时已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>。
    /// </remarks>
    public bool Overwrite { get; init; } = true;

    /// <summary>
    /// 自定义元数据名是否合法。
    /// </summary>
    public static bool IsValidMetadataName(string? name) => name is not null && MetadataName().IsMatch(name);

    /// <summary>
    /// 元数据名的匹配。
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex MetadataName();
}
