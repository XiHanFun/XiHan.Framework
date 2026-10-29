// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 分片上传的清单文件（<c>uploads/&lt;上传标识&gt;/manifest.json</c>）：完成后对象的键与写入选项。
/// </summary>
internal sealed class MultipartManifestDocument
{
    /// <summary>
    /// 完成后对象的键。
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型。
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 自定义元数据。
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 完成时对象已存在是否覆盖。
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// 发起时间；过期清理据此判断。
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
}
