// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Multipart;

/// <summary>
/// 一个已上传的分片：编号、ETag（完成时用来确认拼接的是这一版）与长度。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobPart
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="partNumber">编号（1 到 <see cref="BlobMultipart.MaxPartNumber"/>）。</param>
    /// <param name="eTag">分片的 ETag。</param>
    /// <param name="length">长度（字节）；只用于说明，完成时不校验。</param>
    public BlobPart(int partNumber, string eTag, long length = 0)
    {
        PartNumber = BlobMultipart.ValidatePartNumber(partNumber);
        ETag = !string.IsNullOrWhiteSpace(eTag) ? eTag : throw new ArgumentException("The part ETag must not be empty.", nameof(eTag));
        Length = length >= 0 ? length : throw new ArgumentOutOfRangeException(nameof(length), length, "The part length must not be negative.");
    }

    /// <summary>
    /// 编号。
    /// </summary>
    public int PartNumber { get; }

    /// <summary>
    /// ETag。
    /// </summary>
    public string ETag { get; }

    /// <summary>
    /// 长度（字节）。
    /// </summary>
    public long Length { get; }
}
