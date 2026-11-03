// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Listing;

/// <summary>
/// 列举条件：键前缀、页大小与上一页返回的续页标记
/// </summary>
/// <remarks>
/// 页大小越界不在构造时抛出：查询常由请求绑定而来，越界是调用方可以改正的输入，由 Provider 列举时经 <see cref="Validate"/> 返回 <c>OBJECTSTORAGE_VALIDATION_005</c>（HTTP 400）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobQuery
{
    /// <summary>
    /// 页大小上限
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// 全部对象，一页最多 <see cref="MaxPageSize"/> 条
    /// </summary>
    public static BlobQuery All { get; } = new();

    /// <summary>
    /// 键前缀（序数比较）
    /// </summary>
    /// <remarks>
    /// 空表示全部
    /// </remarks>
    public string Prefix
    {
        get => field;
        init => field = BlobKey.IsValidPrefix(value) ? value : throw new ArgumentException($"Blob key prefix '{value}' is invalid.", nameof(value));
    } = string.Empty;

    /// <summary>
    /// 页大小，1 到 <see cref="MaxPageSize"/>
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="MaxPageSize"/>。越界由 <see cref="Validate"/> 报告
    /// </remarks>
    public int PageSize { get; init; } = MaxPageSize;

    /// <summary>
    /// 续页标记
    /// </summary>
    /// <remarks>
    /// 上一页 <see cref="BlobPage.ContinuationToken"/>；<see langword="null"/> 从头开始
    /// </remarks>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// 核对分页参数
    /// </summary>
    /// <remarks>
    /// Provider 在 <c>ListAsync</c> 开头调用，失败时原样返回、不访问存储
    /// </remarks>
    /// <returns>成功；页大小不在 1 到 <see cref="MaxPageSize"/> 之间时为 <see cref="ObjectStorageErrors.PageSizeInvalid"/> 校验错误</returns>
    public Result Validate()
        => PageSize is >= 1 and <= MaxPageSize
            ? Result.Success()
            : Error.Validation(ObjectStorageErrors.PageSizeInvalid, ObjectStorageMessages.PageSizeInvalid, MessageArguments.Of(("pageSize", PageSize), ("max", MaxPageSize)));
}
