// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 表达式的语法或求值错误；在求值器边界翻译成 <see cref="WorkflowErrors.ExpressionInvalid"/>。
/// </summary>
internal sealed class ExpressionException : Exception
{
    /// <summary>
    /// 创建。
    /// </summary>
    public ExpressionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 创建。
    /// </summary>
    public ExpressionException()
    {
    }

    /// <summary>
    /// 创建。
    /// </summary>
    public ExpressionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
