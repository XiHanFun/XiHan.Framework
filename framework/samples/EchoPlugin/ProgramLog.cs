// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace EchoPlugin;

/// <summary>
/// 入口程序的日志
/// </summary>
internal static partial class ProgramLog
{
    /// <summary>
    /// 记录没有可用的回调端点
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "XIHAN_PLUGIN_HOST_ADDRESS has no supported callback endpoint; callbacks to the host will fail.")]
    public static partial void NoCallbackEndpoint(ILogger logger);

    /// <summary>
    /// 记录收尾时跑停止逻辑
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping on shutdown.")]
    public static partial void StoppingOnShutdown(ILogger logger);
}
