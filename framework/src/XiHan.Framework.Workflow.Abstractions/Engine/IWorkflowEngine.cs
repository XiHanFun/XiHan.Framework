#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IWorkflowEngine
// Guid:aa54e9c2-07b8-4d61-8f35-92c60de41b78
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 工作流引擎（实例生命周期的统一入口）
/// </summary>
/// <remarks>
/// 引擎对同一实例的所有操作以分布式锁保证单写者，可安全并发调用；
/// 实例挂起等待期间不占用任何线程，恢复来源包括：人工任务办理、定时器到期、信号发布、子流程终态回调。
/// </remarks>
public interface IWorkflowEngine
{
    /// <summary>
    /// 启动流程实例（仅允许启动已发布定义）
    /// </summary>
    /// <param name="request">启动请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启动后的实例（同步链路能走多远走多远，可能已直接完成）</returns>
    Task<WorkflowInstance> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复书签（消费指定书签并驱动实例继续执行）
    /// </summary>
    /// <remarks>
    /// 书签一经消费，本次执行批次即以引擎内部令牌推进并保证收尾持久化；
    /// 调用方取消令牌仅在消费书签前生效，批次途中的取消按节点故障处理（可重试），不会丢失实例状态。
    /// </remarks>
    /// <param name="bookmarkId">书签标识</param>
    /// <param name="inputs">恢复输入</param>
    /// <param name="throwIfNotResumable">实例不可恢复（挂起/终态/书签已消费）时是否抛出异常；false 表示静默跳过并保留书签</param>
    /// <param name="expectedBookmarkKey">期望的书签索引键（非空时在锁内校验，键已变化——如任务已转办——则拒绝恢复）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>恢复后的实例</returns>
    Task<WorkflowInstance> ResumeBookmarkAsync(
        string bookmarkId,
        Dictionary<string, object?>? inputs = null,
        bool throwIfNotResumable = true,
        string? expectedBookmarkKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布信号（恢复所有匹配的信号书签）
    /// </summary>
    /// <param name="signalName">信号名称</param>
    /// <param name="payload">信号载荷（作为恢复输入）</param>
    /// <param name="correlationId">业务相关性标识（为空表示广播；书签自身未声明相关性时任意信号均可命中）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功恢复的书签数量</returns>
    Task<int> PublishSignalAsync(
        string signalName,
        Dictionary<string, object?>? payload = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 挂起实例（书签保留但拒绝恢复，直至实例恢复运行）
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="reason">挂起原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例</returns>
    Task<WorkflowInstance> SuspendAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复被挂起的实例
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例</returns>
    Task<WorkflowInstance> ResumeAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消实例（删除书签、取消挂起节点；定义启用补偿时按执行逆序补偿）
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="reason">取消原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例（已处于终态时幂等返回）</returns>
    Task<WorkflowInstance> CancelAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止实例（强制结束，不执行补偿）
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="reason">终止原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例（已处于终态时幂等返回）</returns>
    Task<WorkflowInstance> TerminateAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重试故障实例（从故障节点重新执行）
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例</returns>
    Task<WorkflowInstance> RetryAsync(string instanceId, CancellationToken cancellationToken = default);
}
