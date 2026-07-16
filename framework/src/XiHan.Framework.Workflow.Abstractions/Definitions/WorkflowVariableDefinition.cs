#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowVariableDefinition
// Guid:9b1d4a70-2e63-45f8-8c19-a67e0b52d34f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:07:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流程变量定义（启动入参声明）
/// </summary>
public class WorkflowVariableDefinition
{
    /// <summary>
    /// 变量名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 类型提示（如 string/number/boolean/object，仅供设计器与文档使用，引擎不做强类型校验）
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 是否必填（启动时缺失则拒绝启动）
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 默认值（启动时未提供且非必填时生效）
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }
}
