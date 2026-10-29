// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 要读的字节范围。
/// </summary>
/// <remarks>
/// 从 <see cref="Offset"/> 起 <see cref="Length"/> 个字节，长度为空表示读到末尾；超出末尾的部分截掉。起点必须落在对象内，否则 <see cref="ObjectStorageErrors.RangeNotSatisfiable"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobRange
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="offset">起点（从 0 起）。</param>
    /// <param name="length">长度；为 <see langword="null"/> 表示读到末尾。</param>
    public BlobRange(long offset, long? length = null)
    {
        Offset = offset >= 0 ? offset : throw new ArgumentOutOfRangeException(nameof(offset), offset, "The range offset must not be negative.");
        Length = length is null or > 0 ? length : throw new ArgumentOutOfRangeException(nameof(length), length, "The range length must be positive.");
    }

    /// <summary>
    /// 起点（从 0 起）。
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// 长度。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示读到末尾。
    /// </remarks>
    public long? Length { get; }

    /// <summary>
    /// 按对象的总长度落定范围。
    /// </summary>
    /// <remarks>
    /// 起点不在对象内（含空对象）时为无值，长度超出末尾时截到末尾。
    /// </remarks>
    public Maybe<BlobContentRange> Resolve(long totalLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalLength);
        if (Offset >= totalLength)
        {
            return Maybe.None<BlobContentRange>();
        }

        var available = totalLength - Offset;
        return Maybe.Some(new BlobContentRange(Offset, Length is { } length ? Math.Min(length, available) : available, totalLength));
    }
}
