#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowDefinitionValidationException
// Guid:38c50e12-6f9d-4a74-b8e3-025c1d94f7a6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:43:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
