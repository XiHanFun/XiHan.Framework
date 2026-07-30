// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
