// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Exceptions;

/// <summary>
/// 流程定义校验异常
/// </summary>
public class WorkflowDefinitionValidationException : WorkflowException
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="errors">校验错误集合</param>
    public WorkflowDefinitionValidationException(IReadOnlyList<string> errors)
        : base($"流程定义校验失败：{string.Join("；", errors)}")
    {
        Errors = errors;
    }

    /// <summary>
    /// 校验错误集合
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
