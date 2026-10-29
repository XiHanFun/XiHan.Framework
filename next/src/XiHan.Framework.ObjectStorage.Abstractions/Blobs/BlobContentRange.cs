// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 实际读到的范围。
/// </summary>
/// <remarks>
/// 内容流从 <see cref="Offset"/> 起共 <see cref="Length"/> 个字节，对象总长 <see cref="TotalLength"/>（对应 HTTP 的 <c>Content-Range: bytes Offset-(Offset+Length-1)/TotalLength</c>）。
/// </remarks>
/// <param name="Offset">起点。</param>
/// <param name="Length">长度（至少 1）。</param>
/// <param name="TotalLength">对象总长。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobContentRange(long Offset, long Length, long TotalLength)
{
    /// <summary>
    /// 末字节的位置（含）。
    /// </summary>
    public long Last => Offset + Length - 1;
}
