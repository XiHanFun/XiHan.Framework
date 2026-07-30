// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security.ErrorObfuscation.Constants;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Utilities;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Generators;

/// <summary>
/// XML 错误生成器
/// </summary>
internal static class XmlErrorGenerator
{
    /// <summary>
    /// 生成 XML 格式的错误响应
    /// </summary>
    public static XmlErrorResponse GenerateXml(ErrorInfo error)
    {
        var random = Random.Shared;
        var errorType = ErrorConstants.ErrorTypes[random.Next(ErrorConstants.ErrorTypes.Length)];
        var statusCode = ErrorConstants.HttpStatusCodes[random.Next(ErrorConstants.HttpStatusCodes.Length)];
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var traceId = ErrorUtilities.GenerateTraceId();
        var serverName = ErrorConstants.ServerNames[random.Next(ErrorConstants.ServerNames.Length)];
        var hostname = ErrorUtilities.GenerateHostname();

        return new XmlErrorResponse
        {
            Status = statusCode,
            Type = errorType,
            Message = error.Message,
            Exception = error.ExceptionType,
            Timestamp = timestamp,
            TraceId = traceId,
            Language = error.Language,
            Server = serverName,
            StackTrace = error.StackTrace,
            Metadata = new XmlErrorMetadata
            {
                Hostname = hostname,
                ProcessId = random.Next(1000, 99999),
                ThreadId = random.Next(1, 200)
            }
        };
    }
}
