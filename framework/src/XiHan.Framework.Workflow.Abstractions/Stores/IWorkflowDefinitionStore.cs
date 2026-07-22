// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Stores;

/// <summary>
/// 流程定义存储
/// </summary>
/// <remarks>
/// 默认提供进程内内存实现；持久化实现（数据库/Redis）由应用侧替换注册。
/// </remarks>
public interface IWorkflowDefinitionStore
{
    /// <summary>
    /// 按标识查找定义
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    Task<WorkflowDefinition?> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按编码和版本查找定义
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="version">版本号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    Task<WorkflowDefinition?> FindByVersionAsync(string code, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找编码下最新的已发布定义
    /// </summary>
    /// <remarks>
    /// 语义契约：过滤 <c>Code 匹配 &amp;&amp; Status == Published</c>，按 <c>Version 降序</c> 取第一条。
    /// </remarks>
    /// <param name="code">流程编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    Task<WorkflowDefinition?> FindLatestPublishedAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取编码下的最大版本号
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最大版本号（编码不存在返回 0）</returns>
    Task<int> GetMaxVersionAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询定义列表
    /// </summary>
    /// <param name="code">流程编码（为空表示不过滤）</param>
    /// <param name="status">状态（为空表示不过滤）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义列表（按编码升序、版本降序）</returns>
    Task<List<WorkflowDefinition>> GetListAsync(
        string? code = null,
        WorkflowDefinitionStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入定义
    /// </summary>
    /// <param name="definition">定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task InsertAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新定义
    /// </summary>
    /// <param name="definition">定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除定义
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
