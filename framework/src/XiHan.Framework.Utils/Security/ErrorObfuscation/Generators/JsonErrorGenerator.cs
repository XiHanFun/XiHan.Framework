#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonErrorGenerator
// Guid:2d3e4f5a-7b8c-9d0e-1f2a-3b4c5d6e7f8a
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
/// JSON 错误生成器
/// </summary>
internal static class JsonErrorGenerator
{
    /// <summary>
    /// 生成 JSON 对象格式的错误响应
    /// </summary>
    public static JsonErrorResponse GenerateJsonObject(ErrorInfo error)
    {
        var random = Random.Shared;
        var errorType = ErrorConstants.ErrorTypes[random.Next(ErrorConstants.ErrorTypes.Length)];
        var statusCode = ErrorConstants.HttpStatusCodes[random.Next(ErrorConstants.HttpStatusCodes.Length)];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var traceId = ErrorUtilities.GenerateTraceId();
        var requestId = Guid.NewGuid().ToString();
        var serverName = ErrorConstants.ServerNames[random.Next(ErrorConstants.ServerNames.Length)];
        var dbName = ErrorConstants.DatabaseNames[random.Next(ErrorConstants.DatabaseNames.Length)];
        var hostname = ErrorUtilities.GenerateHostname();

        return new JsonErrorResponse
        {
            Status = statusCode,
            Error = errorType,
            Message = error.Message,
            Exception = error.ExceptionType,
            Timestamp = timestamp,
            TimestampISO = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TraceId = traceId,
            RequestId = requestId,
            Path = ErrorConstants.RequestPaths[random.Next(ErrorConstants.RequestPaths.Length)],
            Method = ErrorUtilities.GetRandomHttpMethod(),
            Language = error.Language,
            Server = serverName,
            Database = dbName,
            StackTrace = error.StackTrace,
            Metadata = new ErrorMetadata
            {
                Hostname = hostname,
                Pid = random.Next(1000, 99999),
                ThreadId = random.Next(1, 200),
                MemoryUsage = $"{random.Next(100, 2000)}MB"
            }
        };
    }

    /// <summary>
    /// 生成 JSON 数组格式的错误响应
    /// </summary>
    public static JsonErrorArrayResponse GenerateJsonArray(IList<ErrorInfo> errors)
    {
        var random = Random.Shared;
        var errorItems = new List<ErrorItem>();

        foreach (var error in errors)
        {
            var errorType = ErrorConstants.ErrorTypes[random.Next(ErrorConstants.ErrorTypes.Length)];
            var statusCode = ErrorConstants.HttpStatusCodes[random.Next(ErrorConstants.HttpStatusCodes.Length)];

            errorItems.Add(new ErrorItem
            {
                Code = statusCode.ToString(),
                Type = errorType,
                Message = error.Message,
                Detail = error.StackTrace,
                Source = new ErrorSource
                {
                    Language = error.Language,
                    Exception = error.ExceptionType
                }
            });
        }

        return new JsonErrorArrayResponse
        {
            Errors = errorItems,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            TraceId = ErrorUtilities.GenerateTraceId(),
            Count = errors.Count
        };
    }
}
