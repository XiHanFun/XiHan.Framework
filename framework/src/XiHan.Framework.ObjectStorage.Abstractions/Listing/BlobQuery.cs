// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Listing;

/// <summary>
/// 列举条件：键前缀、页大小与上一页返回的续页标记
/// </summary>
/// <remarks>
/// 页大小越界不在构造时抛出：查询常由请求绑定而来，越界是调用方可以改正的输入，由 Provider 列举时经 <see cref="Validate"/> 返回 <c>OBJECTSTORAGE_VALIDATION_005</c>（HTTP 400）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record BlobQuery
{
    /// <summary>
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(BlobQuery.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
    /// </remarks>
    public const int Revision = 1;

    /// <summary>
    /// 这次查询用到的条件里最高的修订号
    /// </summary>
    /// <remarks>
    /// 没有用到新条件时不变，旧 Provider 照常服务。5.0 的条件都属于修订 1；以后新增条件时改为实例方法，按设置了的条件取其中最高的修订号，不要直接返回 <see cref="Revision"/>
    /// </remarks>
    private static int RequiredRevision() => 1;

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
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/>，先于其余校验核对</param>
    public Result Validate(int supportedRevision)
    {
        // 先核对修订号：用到了 Provider 不认识的条件时，其余校验无从谈起
        var revision = QueryRevision.Check(nameof(BlobQuery), RequiredRevision(), supportedRevision);
        if (revision.IsFailure)
        {
            return revision;
        }

        return PageSize is >= 1 and <= MaxPageSize
            ? Result.Success()
            : Error.Validation(ObjectStorageErrors.PageSizeInvalid, ObjectStorageMessages.PageSizeInvalid, MessageArguments.Of(("pageSize", PageSize), ("max", MaxPageSize)));
    }
}
