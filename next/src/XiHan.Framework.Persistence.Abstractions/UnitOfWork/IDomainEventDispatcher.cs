// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 领域事件的派发端口。
/// </summary>
/// <remarks>
/// Transaction 阶段在处理器成功后、提交前把工作单元里带有未派发事件的聚合交给它（RFC-0005）。实现由 Messaging 子系统提供（把事件映射成 CloudEvents 信封经 <c>IMessagePublisher</c> 发布，RFC-0009），作用域服务；派发成功后实现负责清空各聚合的缓冲。返回失败时工作单元回滚、调用以该失败结束。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IDomainEventDispatcher
{
    /// <summary>
    /// 派发这些聚合的全部未派发事件。
    /// </summary>
    /// <param name="aggregates">带有未派发事件的聚合，按首次写入顺序。</param>
    /// <param name="tenantId">当前工作单元的租户；无租户为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    ValueTask<Result> DispatchAsync(IReadOnlyList<IHasDomainEvents> aggregates, string? tenantId, CancellationToken cancellationToken);
}
