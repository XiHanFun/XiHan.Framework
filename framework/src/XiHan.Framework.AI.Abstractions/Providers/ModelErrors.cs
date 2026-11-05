// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// 聊天与嵌入共用的模型调用失败
/// </summary>
internal static partial class ModelErrors
{
    /// <summary>
    /// 模型调用失败：记下完整异常，返回模型不可用
    /// </summary>
    /// <remarks>
    /// 这个错误会返回给调用方，而 Provider 可以自定义，异常消息可能带端点地址或凭据片段：<c>reason</c> 只写类型，完整异常只进日志
    /// </remarks>
    public static Error Unavailable(ILogger logger, string provider, Exception exception)
    {
        LogCallFailed(logger, provider, exception);
        var type = exception.GetType();
        return Error.Unavailable(AIErrors.ModelUnavailable, AIMessages.ModelUnavailable, MessageArguments.Of(("provider", provider), ("reason", type.FullName ?? type.Name)));
    }

    /// <summary>
    /// 模型调用失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The call to AI provider '{Provider}' failed.")]
    private static partial void LogCallFailed(ILogger logger, string provider, Exception exception);
}
