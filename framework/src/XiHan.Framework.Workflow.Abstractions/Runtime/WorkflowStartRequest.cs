#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowStartRequest
// Guid:3d81f5c0-a627-4e94-b0d3-16c85a97e2f0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:29:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 流程启动请求
/// </summary>
public class WorkflowStartRequest
{
    /// <summary>
    /// 预分配实例标识（子流程场景由引擎预先铸造，用于父节点隔离回调波次；为空时启动时自动分配）
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// 实例深度（子流程场景由引擎填充：父深度 + 1，超过上限拒绝启动以阻断递归失控）
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// 定义编码（与定义标识二选一，优先使用定义标识）
    /// </summary>
    public string? DefinitionCode { get; set; }

    /// <summary>
    /// 定义版本（为空表示最新已发布版本）
    /// </summary>
    public int? DefinitionVersion { get; set; }

    /// <summary>
    /// 定义标识（直接指定具体定义）
    /// </summary>
    public string? DefinitionId { get; set; }

    /// <summary>
    /// 实例名称（为空时取定义名称）
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 启动变量
    /// </summary>
    public Dictionary<string, object?> Variables { get; set; } = [];

    /// <summary>
    /// 业务相关性标识
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 发起人标识
    /// </summary>
    public string? StarterId { get; set; }

    /// <summary>
    /// 父实例标识（子流程场景由引擎填充）
    /// </summary>
    public string? ParentInstanceId { get; set; }

    /// <summary>
    /// 父节点实例标识（子流程场景由引擎填充）
    /// </summary>
    public string? ParentNodeInstanceId { get; set; }
}
