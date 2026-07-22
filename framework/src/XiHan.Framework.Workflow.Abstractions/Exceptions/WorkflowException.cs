// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
