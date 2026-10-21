// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// 查询规约：同一条件既能在内存判定，也能交给查询提供器翻译。
/// </summary>
/// <typeparam name="T">候选对象类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface ISpecification<T>
{
    /// <summary>
    /// 条件表达式。
    /// </summary>
    Expression<Func<T, bool>> Criteria { get; }

    /// <summary>
    /// 在内存中判定。
    /// </summary>
    bool IsSatisfiedBy(T candidate);
}
