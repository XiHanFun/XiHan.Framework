// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 对象的元数据。
/// </summary>
/// <param name="Key">键。</param>
/// <param name="Length">内容长度（字节）。</param>
/// <param name="ContentType">内容类型。</param>
/// <param name="ETag">内容标签：内容变化即变化，形式由 Provider 决定。</param>
/// <param name="LastModified">最后修改时间。</param>
/// <param name="Metadata">自定义元数据。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobMetadata(BlobKey Key, long Length, string ContentType, string ETag, DateTimeOffset LastModified, IReadOnlyDictionary<string, string> Metadata);
