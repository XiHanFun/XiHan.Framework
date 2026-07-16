#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IWorkflowBookmarkStore
// Guid:912e6a3f-b58c-4d07-a1e9-46f0d8b25c73
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:33:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Stores;

/// <summary>
/// 流程书签存储
/// </summary>
/// <remarks>
/// 默认提供进程内内存实现；持久化实现（数据库/Redis）由应用侧替换注册。
/// 定时器 Worker 已通过分布式锁保证集群单活，实现无需在查询层做原子领取。
/// 查询不做租户过滤，租户隔离由引擎与任务服务在查询结果上按环境租户执行。
/// </remarks>
public interface IWorkflowBookmarkStore
{
    /// <summary>
    /// 按标识查找书签
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签（不存在返回 null）</returns>
    Task<WorkflowBookmark?> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表</returns>
    Task<List<WorkflowBookmark>> GetByInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取节点实例的全部书签
    /// </summary>
    /// <param name="nodeInstanceId">节点实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表</returns>
    Task<List<WorkflowBookmark>> GetByNodeInstanceAsync(string nodeInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取到期的定时类书签
    /// </summary>
    /// <remarks>
    /// 语义契约：过滤 <c>DueTime 非空 &amp;&amp; DueTime &lt;= now</c>；排序 <c>DueTime 升序</c>；最多返回 <paramref name="maxResultCount"/> 条。
    /// </remarks>
    /// <param name="now">当前时间</param>
    /// <param name="maxResultCount">最大返回条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>到期书签列表</returns>
    Task<List<WorkflowBookmark>> GetDueAsync(DateTime now, int maxResultCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按种类和索引键查询书签
    /// </summary>
    /// <param name="kind">书签种类</param>
    /// <param name="key">索引键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    Task<List<WorkflowBookmark>> GetByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询匹配信号的书签
    /// </summary>
    /// <remarks>
    /// 语义契约：过滤 <c>Kind == Signal &amp;&amp; Key == signalName</c>；
    /// <paramref name="correlationId"/> 非空时额外要求 <c>书签 CorrelationId 为空（不限相关性）或与之相等</c>，
    /// 为空时表示广播，不按相关性过滤。
    /// </remarks>
    /// <param name="signalName">信号名称</param>
    /// <param name="correlationId">业务相关性标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    Task<List<WorkflowBookmark>> GetBySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入书签
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task InsertAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新书签
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除书签
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task DeleteByInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
}
