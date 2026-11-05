// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Database;

/// <summary>
/// 数据库实现与调用方事务的关系（作用域服务）
/// </summary>
/// <remarks>
/// 在调用里、Transaction 阶段已开启工作单元时加入它：存储的读写、活动与结果处理器的写入、经发件箱发出的事件都随调用方提交或回滚。调用之外、或工作单元还没开始（Tenant / Authorize 阶段里）、已经结束时不加入，引擎自己开一次调用。只读的工作单元里写入以 <see cref="PersistenceErrors.ReadOnly"/> 拒绝
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowTransaction : IWorkflowTransaction
{
    /// <summary>
    /// 本作用域的工作单元
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 本作用域的调用上下文访问器
    /// </summary>
    private readonly IInvocationAccessor? _invocation;

    /// <summary>
    /// 创建（作用域服务）
    /// </summary>
    public DatabaseWorkflowTransaction(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _unitOfWork = services.GetRequiredService<IUnitOfWork>();
        _invocation = services.GetService<IInvocationAccessor>();
    }

    /// <inheritdoc />
    public bool IsActive => _invocation?.Current.HasValue == true && _unitOfWork.State == UnitOfWorkState.Active;

    /// <inheritdoc />
    public Result EnsureWritable()
        => _unitOfWork.IsReadOnly ? Result.Failure(Error.Unexpected(PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly)) : Result.Success();
}
