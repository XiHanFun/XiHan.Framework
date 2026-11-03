// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Queries;

/// <summary>
/// 查询的取出条数超过上限
/// </summary>
/// <remarks>
/// 携带 <see cref="PersistenceErrors.TakeExceeded"/>，对外就是一个 <see cref="FrameworkException"/>。<see cref="EntityQuery{T}.Take"/> 只能返回值或抛出，Transaction 阶段的 <see cref="UnitOfWork.UnitOfWorkMiddleware"/> 认出它后取请求的条数，转为带 <c>take</c> 与 <c>max</c> 参数的校验结果
/// </remarks>
/// <param name="take">请求的取出条数</param>
internal sealed class TakeExceededException(int take)
    : FrameworkException(PersistenceErrors.TakeExceeded, string.Create(CultureInfo.InvariantCulture, $"'Take' is {take}, more than the maximum of {EntityQuery.DefaultMaxTake}; page through larger results."))
{
    /// <summary>
    /// 请求的取出条数
    /// </summary>
    public int Take { get; } = take;
}
