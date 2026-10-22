// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 元数据文件（<c>meta/&lt;key&gt;.json</c>）的内容。
/// </summary>
internal sealed class BlobMetadataDocument
{
    /// <summary>
    /// 内容长度。
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// 内容类型。
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 内容的 SHA-256（小写十六进制）。
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// 最后修改时间。
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// 自定义元数据。
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}
