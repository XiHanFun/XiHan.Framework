// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Listing;

/// <summary>
/// 列举的一页。
/// </summary>
/// <remarks>
/// 按键的序数顺序；还有下一页时带续页标记。
/// </remarks>
/// <param name="Items">本页对象的元数据。</param>
/// <param name="ContinuationToken">下一页的标记；没有下一页为 <see langword="null"/>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobPage(IReadOnlyList<BlobMetadata> Items, string? ContinuationToken);
