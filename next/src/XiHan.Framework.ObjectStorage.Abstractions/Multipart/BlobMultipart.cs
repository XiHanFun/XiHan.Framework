// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Multipart;

/// <summary>
/// 分片上传的限制（与 S3 一致，各 Provider 统一按它校验）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class BlobMultipart
{
    /// <summary>
    /// 除最后一片外每片的最小字节数：5 MiB。
    /// </summary>
    public const long MinPartBytes = 5L << 20;

    /// <summary>
    /// 最大分片编号：10000。
    /// </summary>
    public const int MaxPartNumber = 10_000;

    /// <summary>
    /// 校验分片编号在 1 到 <see cref="MaxPartNumber"/> 之间。
    /// </summary>
    /// <remarks>
    /// 越界是编程错误，抛出。
    /// </remarks>
    public static int ValidatePartNumber(int partNumber)
        => partNumber is >= 1 and <= MaxPartNumber ? partNumber : throw new ArgumentOutOfRangeException(nameof(partNumber), partNumber, $"The part number must be between 1 and {MaxPartNumber}.");
}
