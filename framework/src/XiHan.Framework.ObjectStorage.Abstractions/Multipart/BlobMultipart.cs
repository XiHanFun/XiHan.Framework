// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Multipart;

/// <summary>
/// 分片上传的限制（与 S3 一致，各 Provider 统一按它校验）
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class BlobMultipart
{
    /// <summary>
    /// 除最后一片外每片的最小字节数：5 MiB
    /// </summary>
    public const long MinPartBytes = 5L << 20;

    /// <summary>
    /// 最大分片编号：10000
    /// </summary>
    public const int MaxPartNumber = 10_000;

    /// <summary>
    /// 校验分片编号在 1 到 <see cref="MaxPartNumber"/> 之间
    /// </summary>
    /// <remarks>
    /// 越界是编程错误，抛出
    /// </remarks>
    public static int ValidatePartNumber(int partNumber)
        => partNumber is >= 1 and <= MaxPartNumber ? partNumber : throw new ArgumentOutOfRangeException(nameof(partNumber), partNumber, $"The part number must be between 1 and {MaxPartNumber}.");

    /// <summary>
    /// 校验完成时的分片清单本身：非空、编号严格升序
    /// </summary>
    /// <remarks>
    /// 只看清单，不碰存储：各 Provider 在访问存储之前先调用它，同一份清单在哪个存储上都给出同一个错误。为空或乱序返回 <see cref="ObjectStorageErrors.PartsInvalid"/>；清单里有 <see langword="null"/> 是编程错误，抛出。分片是否上传、ETag 是否相符、非末片是否过小要看存储，由 Provider 接着判定
    /// </remarks>
    /// <param name="parts">完成清单</param>
    /// <exception cref="ArgumentException">清单里有 <see langword="null"/></exception>
    public static Result ValidateParts(IReadOnlyList<BlobPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsEmpty);
        }

        var previous = 0;
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index] ?? throw new ArgumentException("The part list must not contain null.", nameof(parts));
            if (index > 0 && part.PartNumber <= previous)
            {
                return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsUnordered, MessageArguments.Of(("part", part.PartNumber)));
            }

            previous = part.PartNumber;
        }

        return Result.Success();
    }

    /// <summary>
    /// 校验批量放弃的上传集合本身：不含 <see langword="null"/>
    /// </summary>
    /// <remarks>
    /// 只看集合，不碰存储：各 Provider 在放弃任何上传之前先调用它，集合里有 <see langword="null"/> 时在哪个存储上都是动手之前抛出同一个异常，不会放弃了前几个才发现。空集合合法
    /// </remarks>
    /// <param name="uploads">要放弃的上传</param>
    /// <exception cref="ArgumentException">集合里有 <see langword="null"/></exception>
    public static void ValidateUploads(IReadOnlyList<BlobMultipartUpload> uploads)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        for (var index = 0; index < uploads.Count; index++)
        {
            if (uploads[index] is null)
            {
                throw new ArgumentException("The upload list must not contain null.", nameof(uploads));
            }
        }
    }
}
