// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 对象的元数据
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobMetadata
{
    /// <summary>
    /// 键
    /// </summary>
    public required BlobKey Key { get; init; }

    /// <summary>
    /// 内容长度（字节）
    /// </summary>
    public required long Length { get; init; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// 内容标签：内容变化即变化，形式由 Provider 决定
    /// </summary>
    public required string ETag { get; init; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public required DateTimeOffset LastModified { get; init; }

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
