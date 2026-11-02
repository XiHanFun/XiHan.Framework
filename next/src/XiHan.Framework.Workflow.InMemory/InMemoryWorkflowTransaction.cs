// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 内存实现与调用方事务的关系（作用域服务）。
/// </summary>
/// <remarks>
/// 内存存储没有事务，也加入不了调用方的持久化事务：只要在调用里就在里面执行，写入立即生效，调用方回滚不撤销。引擎仍在调用里执行活动、把实例锁持有到作用域结束，语义上与数据库实现的差别只在"不随调用方提交或回滚"。只读的调用（操作契约只读）里写入以 <see cref="PersistenceErrors.ReadOnly"/> 拒绝，与数据库实现相同（ADR-0099）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowTransaction : IWorkflowTransaction
{
    /// <summary>
    /// 本作用域的调用上下文访问器。
    /// </summary>
    private readonly IInvocationAccessor? _invocation;

    /// <summary>
    /// 创建（作用域服务）。
    /// </summary>
    public InMemoryWorkflowTransaction(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _invocation = services.GetService<IInvocationAccessor>();
    }

    /// <inheritdoc />
    public bool IsActive => _invocation?.Current.HasValue == true;

    /// <inheritdoc />
    /// <remarks>
    /// 判定与 Transaction 阶段开只读工作单元的判定相同：调用带操作描述且契约只读（动词 <c>get</c> / <c>list</c> 或声明了只读）。没有装持久化、没有工作单元时同样拒绝。
    /// </remarks>
    public Result EnsureWritable()
    {
        // 内存实现不加入工作单元，不能像数据库实现那样问工作单元是否只读；直接看调用的操作契约——它正是 Transaction 阶段决定开只读工作单元的依据，
        // 两种实现因此在同一类调用里拒绝，且都在取锁、执行活动之前（WorkflowScope 先调这里），只读调用里活动的副作用不会先发生
        var readOnly = _invocation?.Current.TryGetValue(out var context) == true
            && context.GetOperation().Match(static operation => operation.Contract.IsReadOnly, static () => false);
        return readOnly ? Result.Failure(Error.Unexpected(PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly)) : Result.Success();
    }
}
