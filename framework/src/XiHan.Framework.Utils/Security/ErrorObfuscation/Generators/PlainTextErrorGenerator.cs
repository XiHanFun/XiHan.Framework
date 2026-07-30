// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security.ErrorObfuscation.Constants;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Utilities;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Generators;

/// <summary>
/// 纯文本错误生成器
/// </summary>
internal static class PlainTextErrorGenerator
{
    /// <summary>
    /// 生成纯文本格式的错误响应
    /// </summary>
    public static PlainTextErrorResponse GeneratePlainText(ErrorInfo error)
    {
        var random = Random.Shared;
        var errorType = ErrorConstants.ErrorTypes[random.Next(ErrorConstants.ErrorTypes.Length)];
        var statusCode = ErrorConstants.HttpStatusCodes[random.Next(ErrorConstants.HttpStatusCodes.Length)];
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var traceId = ErrorUtilities.GenerateTraceId();
        var serverName = ErrorConstants.ServerNames[random.Next(ErrorConstants.ServerNames.Length)];
        var hostname = ErrorUtilities.GenerateHostname();

        return new PlainTextErrorResponse
        {
            Time = timestamp,
            Status = statusCode,
            Type = errorType,
            Language = error.Language,
            Server = serverName,
            TraceId = traceId,
            Host = hostname,
            Message = error.Message,
            StackTrace = error.StackTrace
        };
    }
}
