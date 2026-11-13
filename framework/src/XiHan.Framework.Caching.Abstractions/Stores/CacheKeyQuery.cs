// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 键列表的查询
/// </summary>
/// <remarks>
/// 前缀不合法与每页条数越界都不在构造时抛出：查询常由管理界面的请求绑定而来，这些是调用方可以改正的输入，由存储列出时经 <see cref="Validate"/> 返回 <see cref="CachingErrors.PrefixInvalid"/> 或 <see cref="CachingErrors.ListPageSizeInvalid"/>（HTTP 400）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheKeyQuery
{
    /// <summary>
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(CacheKeyQuery.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
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
    /// 每页条数的最大值
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// 每页条数的缺省值
    /// </summary>
    public const int DefaultPageSize = 100;

    /// <summary>
    /// 键的前缀
    /// </summary>
    /// <remarks>
    /// 空为全部；与键同文法（无空白与控制字符，不超过 <see cref="CacheKey.MaxLength"/>），区分大小写。不合法由 <see cref="Validate"/> 报告
    /// </remarks>
    public string Prefix
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = string.Empty;

    /// <summary>
    /// 续页标记
    /// </summary>
    /// <remarks>
    /// 上一页的 <see cref="CacheKeyPage.ContinuationToken"/>；第一页为 <see langword="null"/>
    /// </remarks>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// 每页条数，1 到 <see cref="MaxPageSize"/>
    /// </summary>
    /// <remarks>
    /// 缺省 <see cref="DefaultPageSize"/>；Redis 实现把它作为 SCAN 的提示值。越界由 <see cref="Validate"/> 报告
    /// </remarks>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// 核对前缀与每页条数
    /// </summary>
    /// <remarks>
    /// 存储在 <c>ListKeysAsync</c> 开头调用，失败时原样返回、不访问后端
    /// </remarks>
    /// <returns>成功；前缀不合法时为 <see cref="CachingErrors.PrefixInvalid"/>，条数不在 1 到 <see cref="MaxPageSize"/> 之间时为 <see cref="CachingErrors.ListPageSizeInvalid"/></returns>
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/>，先于其余校验核对</param>
    public Result Validate(int supportedRevision)
    {
        // 先核对修订号：用到了 Provider 不认识的条件时，其余校验无从谈起
        var revision = QueryRevision.Check(nameof(CacheKeyQuery), RequiredRevision(), supportedRevision);
        if (revision.IsFailure)
        {
            return revision;
        }

        if (!CacheKey.IsValidPrefix(Prefix))
        {
            return PrefixInvalid(Prefix);
        }

        return PageSize is >= 1 and <= MaxPageSize
            ? Result.Success()
            : Error.Validation(CachingErrors.ListPageSizeInvalid, CachingMessages.ListPageSizeInvalid, MessageArguments.Of(("pageSize", PageSize), ("max", MaxPageSize)));
    }

    /// <summary>
    /// 核对按前缀删除的前缀
    /// </summary>
    /// <remarks>
    /// 存储在 <c>RemoveByPrefixAsync</c> 开头调用，失败时原样返回、不访问后端。删除不接受空前缀，防止误删全部
    /// </remarks>
    /// <returns>成功；空前缀与不合法的前缀为 <see cref="CachingErrors.PrefixInvalid"/>，空前缀的消息是 <see cref="CachingMessages.PrefixRequired"/></returns>
    public static Result ValidateRemovalPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.Length == 0)
        {
            return Error.Validation(CachingErrors.PrefixInvalid, CachingMessages.PrefixRequired);
        }

        return CacheKey.IsValidPrefix(prefix) ? Result.Success() : PrefixInvalid(prefix);
    }

    /// <summary>
    /// 前缀不合法的错误
    /// </summary>
    private static Error PrefixInvalid(string prefix)
        => Error.Validation(CachingErrors.PrefixInvalid, CachingMessages.PrefixInvalid, MessageArguments.Of(("prefix", prefix), ("max", CacheKey.MaxLength)));
}
