// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 工作流存储与调用方事务的关系（作用域服务，Provider 实现）。
/// </summary>
/// <remarks>
/// 引擎、定义目录、任务服务与外部任务服务每个操作先问它：<see cref="IsActive"/> 为真时在调用方的事务里执行——存储的读写、活动与结果处理器的写入、发出的事件都随调用方提交或回滚，实例锁持有到调用方的服务作用域结束；为假时自己开一次经管道的调用（新的服务作用域、调用方的租户），操作在那次调用的事务里执行。数据库实现：调用里 Transaction 阶段已开启的工作单元；内存实现没有事务，只要在调用里就算（写入立即生效，回滚不撤销）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowTransaction
{
    /// <summary>
    /// 当前作用域在不在可以加入的调用方事务里。
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// 核对加入的事务是否允许写入。
    /// </summary>
    /// <remarks>
    /// 允许时成功，只读事务里返回拒绝写入的错误。写入操作在取锁、执行活动之前调用，只读的调用不会先执行活动的副作用再在写回时失败。数据库实现返回 <c>PERSISTENCE_DENIED_001</c>；内存实现总是成功。
    /// </remarks>
    Result EnsureWritable();
}
