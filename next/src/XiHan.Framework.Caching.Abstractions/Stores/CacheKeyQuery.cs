// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 键列表的查询
/// </summary>
/// <remarks>
/// 每页上限越界不在构造时抛出：查询常由管理界面的请求绑定而来，越界是调用方可以改正的输入，由存储列出时经 <see cref="Validate"/> 返回 <see cref="CachingErrors.ListLimitInvalid"/>（HTTP 400）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheKeyQuery
{
    /// <summary>
    /// 每页上限的最大值
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 键的前缀
    /// </summary>
    /// <remarks>
    /// 空为全部；与键同文法（无空白与控制字符，不超过 <see cref="CacheKey.MaxLength"/>），区分大小写
    /// </remarks>
    public string Prefix
    {
        get => field;
        init => field = CacheKey.IsValidPrefix(value) ? value : throw new ArgumentException($"Cache key prefix '{value}' is invalid: it must contain no whitespace or control characters and be at most {CacheKey.MaxLength} characters long.", nameof(value));
    } = string.Empty;

    /// <summary>
    /// 续页标记
    /// </summary>
    /// <remarks>
    /// 上一页的 <see cref="CacheKeyPage.Continuation"/>；第一页为 <see langword="null"/>
    /// </remarks>
    public string? Continuation { get; init; }

    /// <summary>
    /// 每页上限，1 到 <see cref="MaxLimit"/>
    /// </summary>
    /// <remarks>
    /// 默认 100；Redis 实现把它作为 SCAN 的提示值。越界由 <see cref="Validate"/> 报告
    /// </remarks>
    public int Limit { get; init; } = 100;

    /// <summary>
    /// 核对每页上限
    /// </summary>
    /// <remarks>
    /// 存储在 <c>ListKeysAsync</c> 开头调用，失败时原样返回、不访问后端
    /// </remarks>
    /// <returns>成功；上限不在 1 到 <see cref="MaxLimit"/> 之间时为 <see cref="CachingErrors.ListLimitInvalid"/> 校验错误</returns>
    public Result Validate()
        => Limit is >= 1 and <= MaxLimit
            ? Result.Success()
            : Error.Validation(CachingErrors.ListLimitInvalid, CachingMessages.ListLimitInvalid, MessageArguments.Of(("limit", Limit), ("max", MaxLimit)));
}
