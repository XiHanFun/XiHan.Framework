// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 写入选项：内容类型、自定义元数据（名 <c>^[a-z0-9][a-z0-9-]{0,63}$</c>）、是否允许覆盖
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record BlobPutOptions
{
    /// <summary>
    /// 默认内容类型
    /// </summary>
    public const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// 内容类型的最大长度
    /// </summary>
    public const int MaxContentTypeLength = 255;

    /// <summary>
    /// 自定义元数据的名与值合计的最大 UTF-8 字节数
    /// </summary>
    /// <remarks>
    /// 与 S3 用户元数据的上限一致（ADR-0214）
    /// </remarks>
    public const int MaxMetadataBytes = 2048;

    /// <summary>
    /// 默认选项：二进制、无元数据、允许覆盖
    /// </summary>
    public static BlobPutOptions Default { get; } = new();

    /// <summary>
    /// 内容类型
    /// </summary>
    /// <remarks>
    /// 须满足 <see cref="IsValidContentType"/>：它会进 S3 的请求头与文件系统的元数据文件，换行能拆出别的头（ADR-0214）
    /// </remarks>
    public string ContentType
    {
        get => field;
        init => field = IsValidContentType(value) ? value : throw new ArgumentException($"The content type must be a media type 'type/subtype' with optional ';' parameters of printable ASCII, at most {MaxContentTypeLength} characters.", nameof(value));
    } = DefaultContentType;

    /// <summary>
    /// 自定义元数据
    /// </summary>
    /// <remarks>
    /// 名须满足 <see cref="IsValidMetadataName"/>；值不含控制字符；名与值合计不超过 <see cref="MaxMetadataBytes"/> 个 UTF-8 字节（ADR-0214）
    /// </remarks>
    public IReadOnlyDictionary<string, string> Metadata
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            var total = 0;
            foreach (var (name, text) in value)
            {
                if (!IsValidMetadataName(name))
                {
                    throw new ArgumentException($"Metadata name '{name}' is invalid: it must be 1 to 64 lowercase letters, digits or hyphens, starting with a letter or digit.", nameof(value));
                }

                ArgumentNullException.ThrowIfNull(text, nameof(value));
                // 值进 S3 的请求头：CR、LF 能拆出别的头，其余控制字符也没有合理的用途
                if (text.AsSpan().ContainsAnyInRange('\u0000', '\u001F') || text.AsSpan().ContainsAnyInRange('\u007F', '\u009F'))
                {
                    throw new ArgumentException($"Metadata '{name}' contains control characters.", nameof(value));
                }

                // 名是 ASCII，字节数就是长度
                total += name.Length + Encoding.UTF8.GetByteCount(text);
                if (total > MaxMetadataBytes)
                {
                    throw new ArgumentException($"The metadata names and values must total at most {MaxMetadataBytes} UTF-8 bytes.", nameof(value));
                }
            }

            field = value;
        }
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 已存在时是否覆盖
    /// </summary>
    /// <remarks>
    /// 为 <see langword="false"/> 时已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>
    /// </remarks>
    public bool Overwrite { get; init; } = true;

    /// <summary>
    /// 内容类型是否合法
    /// </summary>
    /// <remarks>
    /// 媒体类型 <c>type/subtype</c>，两段都是 HTTP 的 token 字符，可跟 <c>;</c> 起头的参数（可打印 ASCII 与空格），整体 1 到 <see cref="MaxContentTypeLength"/> 个字符
    /// </remarks>
    public static bool IsValidContentType([NotNullWhen(true)] string? contentType)
        => contentType is { Length: > 0 and <= MaxContentTypeLength } && MediaType().IsMatch(contentType);

    /// <summary>
    /// 自定义元数据名是否合法
    /// </summary>
    public static bool IsValidMetadataName(string? name) => name is not null && MetadataName().IsMatch(name);

    /// <summary>
    /// 元数据名的匹配
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex MetadataName();

    /// <summary>
    /// 媒体类型的匹配
    /// </summary>
    /// <remarks>
    /// token 字符取 RFC 9110；参数部分不细分，只限可打印 ASCII 与空格，不含制表与换行
    /// </remarks>
    [GeneratedRegex(@"^[!#$%&'*+.^_`|~0-9A-Za-z-]+/[!#$%&'*+.^_`|~0-9A-Za-z-]+(?: *;[\x20-\x7E]*)?\z")]
    private static partial Regex MediaType();
}
