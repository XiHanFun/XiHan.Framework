// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流程定义管理器（草稿/发布/版本/停用的生命周期管理）
/// </summary>
public interface IWorkflowDefinitionManager
{
    /// <summary>
    /// 创建定义草稿（自动分配标识；版本号取编码下最大版本 + 1）
    /// </summary>
    /// <param name="definition">定义内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建后的定义</returns>
    Task<WorkflowDefinition> CreateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新草稿定义（仅草稿可更新，已发布定义不可变）
    /// </summary>
    /// <param name="definition">定义内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的定义</returns>
    Task<WorkflowDefinition> UpdateDraftAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布定义（发布前执行结构校验，校验失败抛出校验异常）
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发布后的定义</returns>
    Task<WorkflowDefinition> PublishAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 基于最新版本创建新草稿版本
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新草稿定义</returns>
    Task<WorkflowDefinition> CreateNewVersionAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停用定义（不可启动新实例，存量实例不受影响）
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停用后的定义</returns>
    Task<WorkflowDefinition> DisableAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 归档定义
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>归档后的定义</returns>
    Task<WorkflowDefinition> ArchiveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除定义（仅草稿可删除）
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取定义（不存在抛出异常）
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义</returns>
    Task<WorkflowDefinition> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取已发布定义（版本为空取最新已发布版本；不存在抛出异常）
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="version">版本号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义</returns>
    Task<WorkflowDefinition> GetPublishedAsync(string code, int? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询定义列表
    /// </summary>
    /// <param name="code">流程编码（为空表示不过滤）</param>
    /// <param name="status">状态（为空表示不过滤）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义列表</returns>
    Task<List<WorkflowDefinition>> GetListAsync(
        string? code = null,
        WorkflowDefinitionStatus? status = null,
        CancellationToken cancellationToken = default);
}
