// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions;

/// <summary>
/// 批量写入里一项的结果。
/// </summary>
/// <param name="Key">键。</param>
/// <param name="Outcome">写入后的元数据，或失败。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobPutResult(BlobKey Key, Result<BlobMetadata> Outcome);
