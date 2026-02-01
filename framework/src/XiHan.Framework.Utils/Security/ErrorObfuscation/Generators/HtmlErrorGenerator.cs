#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HtmlErrorGenerator
// Guid:5a6b7c8d-0e1f-2a3b-4c5d-6e7f8a9b0c1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Security.ErrorObfuscation.Constants;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Utilities;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Generators;

/// <summary>
/// HTML 错误生成器
/// </summary>
internal static class HtmlErrorGenerator
{
    /// <summary>
    /// 生成 HTML 格式的错误响应
    /// </summary>
    public static HtmlErrorResponse GenerateHtml(ErrorInfo error)
    {
        var random = Random.Shared;
        var errorType = ErrorConstants.ErrorTypes[random.Next(ErrorConstants.ErrorTypes.Length)];
        var statusCode = ErrorConstants.HttpStatusCodes[random.Next(ErrorConstants.HttpStatusCodes.Length)];
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var traceId = ErrorUtilities.GenerateTraceId();
        var serverName = ErrorConstants.ServerNames[random.Next(ErrorConstants.ServerNames.Length)];
        var hostname = ErrorUtilities.GenerateHostname();

        return new HtmlErrorResponse
        {
            ErrorType = errorType,
            StatusCode = statusCode,
            Message = error.Message,
            ExceptionType = error.ExceptionType,
            Language = error.Language,
            Timestamp = timestamp,
            TraceId = traceId,
            Server = serverName,
            Hostname = hostname,
            StackTrace = error.StackTrace
        };
    }
}
