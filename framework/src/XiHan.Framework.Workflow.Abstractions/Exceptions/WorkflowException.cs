#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowException
// Guid:76b2e4d8-a951-4f30-8c67-d24f10e95ab7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:42:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Exceptions;

/// <summary>
/// 工作流异常（引擎协议错误：定义未发布、实例不可恢复、书签已消费、表达式非法等）
/// </summary>
public class WorkflowException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常信息</param>
    public WorkflowException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常信息</param>
    /// <param name="innerException">内部异常</param>
    public WorkflowException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
