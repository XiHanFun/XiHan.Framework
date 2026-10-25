// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Listing;

/// <summary>
/// 列举条件：键前缀、页大小与上一页返回的续页标记。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobQuery
{
    /// <summary>
    /// 页大小上限。
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// 全部对象，一页最多 <see cref="MaxPageSize"/> 条。
    /// </summary>
    public static BlobQuery All { get; } = new();

    /// <summary>
    /// 键前缀（序数比较）；空表示全部。
    /// </summary>
    public string Prefix
    {
        get => field;
        init => field = BlobKey.IsValidPrefix(value) ? value : throw new ArgumentException($"Blob key prefix '{value}' is invalid.", nameof(value));
    } = string.Empty;

    /// <summary>
    /// 页大小，1 到 <see cref="MaxPageSize"/>。
    /// </summary>
    public int PageSize
    {
        get => field;
        init => field = value is >= 1 and <= MaxPageSize ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The page size must be between 1 and {MaxPageSize}.");
    } = MaxPageSize;

    /// <summary>
    /// 续页标记：上一页 <see cref="BlobPage.ContinuationToken"/>；<see langword="null"/> 从头开始。
    /// </summary>
    public string? ContinuationToken { get; init; }
}
