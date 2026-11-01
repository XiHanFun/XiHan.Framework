// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 内存实现与调用方事务的关系（作用域服务）。
/// </summary>
/// <remarks>
/// 内存存储没有事务，也加入不了调用方的持久化事务：只要在调用里就在里面执行，写入立即生效，调用方回滚不撤销；不看调用是否只读。引擎仍在调用里执行活动、把实例锁持有到作用域结束，语义上与数据库实现的差别只在"不随调用方提交或回滚"。
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
    public Result EnsureWritable() => Result.Success();
}
