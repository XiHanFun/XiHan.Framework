// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 落在存储里的故障：错误码与按进程默认区域渲染的消息。
/// </summary>
/// <param name="Code">错误码。</param>
/// <param name="Message">消息。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowFault(string Code, string Message)
{
    /// <summary>
    /// 从错误创建。
    /// </summary>
    public static WorkflowFault From(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WorkflowFault(error.Code, error.Message);
    }

    /// <summary>
    /// 转回错误（线上还原，文本原样）。
    /// </summary>
    public Error ToError() => Error.Literal(Code, Message);
}
