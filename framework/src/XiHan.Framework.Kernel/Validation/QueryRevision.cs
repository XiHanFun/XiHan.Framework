// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Validation;

/// <summary>
/// 核对查询用到的条件 Provider 是否都认识
/// </summary>
/// <remarks>
/// RFC-0001「Provider 契约的演进」（ADR-0228）：交给 Provider 的查询各有修订号常量 <c>Revision</c>，以后新增条件时加 1；查询按这次实际用到的条件给出所需修订号。Provider 在入口传入编译时取到的常量，常量在编译时写进 Provider，所以旧 Provider 带着的是旧值，遇到新条件即失败，不会读不认识的条件、给出放宽了的结果。各查询的 <c>Validate(int)</c> 转调本方法；应用自己定义的、交给自己 Provider 的查询也可以照此使用
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class QueryRevision
{
    /// <summary>
    /// 所需修订号不高于 Provider 支持的修订号即成功
    /// </summary>
    /// <param name="query">查询类型名，进错误参数，如 <c>AuditQuery</c></param>
    /// <param name="required">查询这次用到的条件里最高的修订号，不小于 1</param>
    /// <param name="supported">Provider 编译时取到的修订号常量</param>
    /// <returns>成功；所需修订号更高时为 <see cref="KernelErrors.QueryRevisionUnsupported"/></returns>
    public static Result Check(string query, int required, int supported)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(required, 1);
        return required <= supported
            ? Result.Success()
            : Result.Failure(Error.Validation(
                KernelErrors.QueryRevisionUnsupported,
                KernelMessages.QueryRevisionUnsupported,
                MessageArguments.Of(("query", query), ("required", required), ("supported", supported))));
    }
}
