#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IWorkflowInstanceStore
// Guid:58b21c96-e4d7-4a03-bf68-207d95c3ea41
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:32:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Stores;

/// <summary>
/// 流程实例存储（实例聚合 + 节点实例执行历史）
/// </summary>
/// <remarks>
/// 默认提供进程内内存实现；持久化实现（数据库/Redis）由应用侧替换注册。
/// 引擎对同一实例的读写已由实例级分布式锁串行化，存储实现无需再做乐观并发控制。
/// </remarks>
public interface IWorkflowInstanceStore
{
    /// <summary>
    /// 按标识查找实例
    /// </summary>
    /// <param name="id">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例（不存在返回 null）</returns>
    Task<WorkflowInstance?> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询实例列表
    /// </summary>
    /// <param name="status">状态（为空表示不过滤）</param>
    /// <param name="definitionCode">定义编码（为空表示不过滤）</param>
    /// <param name="correlationId">业务相关性标识（为空表示不过滤）</param>
    /// <param name="maxResultCount">最大返回条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例列表（按创建时间降序）</returns>
    Task<List<WorkflowInstance>> GetListAsync(
        WorkflowInstanceStatus? status = null,
        string? definitionCode = null,
        string? correlationId = null,
        int maxResultCount = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取实例的直接子实例列表
    /// </summary>
    /// <param name="parentInstanceId">父实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>子实例列表（按创建时间升序）</returns>
    Task<List<WorkflowInstance>> GetChildrenAsync(string parentInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入实例
    /// </summary>
    /// <param name="instance">实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task InsertAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实例
    /// </summary>
    /// <param name="instance">实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除实例（级联删除节点实例）
    /// </summary>
    /// <param name="id">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标识查找节点实例
    /// </summary>
    /// <param name="id">节点实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点实例（不存在返回 null）</returns>
    Task<WorkflowNodeInstance?> FindNodeInstanceAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取实例的节点实例列表（执行历史）
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点实例列表（按开始时间升序，同刻按创建先后；补偿逆序依赖该顺序）</returns>
    Task<List<WorkflowNodeInstance>> GetNodeInstancesAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入节点实例
    /// </summary>
    /// <param name="nodeInstance">节点实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task InsertNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新节点实例
    /// </summary>
    /// <param name="nodeInstance">节点实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task UpdateNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default);
}
