#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorObfuscationHelper
// Guid:6b7c8d9e-1f2a-3b4c-5d6e-7f8a9b0c1d2e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Security.ErrorObfuscation.Generators;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Options;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Utilities;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation;

/// <summary>
/// 错误消息混淆帮助类，用于生成混淆的错误信息以迷惑恶意访问者
/// </summary>
/// <remarks>
/// 当检测到未加白名单的IP频繁访问或恶意行为时，可以使用此类生成各种形式的混淆错误信息。
/// <br/>
/// <br/>支持特性：
/// <list type="bullet">
/// <item>多种编程语言：C#、Java、PHP、Go、Python、Node.js、Ruby、Rust</item>
/// <item>多种输出格式：JSON对象、JSON数组、纯文本、XML、HTML错误页面</item>
/// <item>真实的错误堆栈和异常信息</item>
/// <item>随机生成的追踪ID、时间戳、服务器信息等</item>
/// <item>返回强类型对象，支持序列化为字符串</item>
/// </list>
/// <br/>
/// <br/>使用示例：
/// <code>
/// // 示例 1: 生成随机格式的混淆错误对象
/// object errorObj = ErrorObfuscationHelper.GenerateObfuscatedError();
///
/// // 示例 2: 生成指定格式的混淆错误对象
/// JsonErrorResponse jsonError = ErrorObfuscationHelper.GenerateObfuscatedError&lt;JsonErrorResponse&gt;(ErrorFormat.JsonObject);
/// HtmlErrorResponse htmlError = ErrorObfuscationHelper.GenerateObfuscatedError&lt;HtmlErrorResponse&gt;(ErrorFormat.Html);
///
/// // 示例 3: 生成错误对象并序列化为字符串
/// string errorString = ErrorObfuscationHelper.GenerateObfuscatedErrorAsString();
///
/// // 示例 4: 生成包含Content-Type的响应（返回对象）
/// var (errorObj, contentType) = ErrorObfuscationHelper.GenerateObfuscatedErrorResponse();
/// string content = ErrorResponseSerializer.Serialize(errorObj);
///
/// // 示例 5: 生成包含Content-Type的响应（直接返回字符串）
/// var (content, contentType) = ErrorObfuscationHelper.GenerateObfuscatedErrorResponseAsString();
/// // 在ASP.NET Core中使用:
/// // return Content(content, contentType);
///
/// // 示例 6: 在中间件中使用，检测恶意IP
/// if (IsBlacklistedIp(clientIp))
/// {
///     var (errorObj, contentType) = ErrorObfuscationHelper.GenerateObfuscatedErrorResponse();
///     var errorContent = ErrorResponseSerializer.Serialize(errorObj);
///     context.Response.ContentType = contentType;
///     context.Response.StatusCode = 500;
///     await context.Response.WriteAsync(errorContent);
///     return;
/// }
///
/// // 示例 7: 带延迟的错误响应（增加攻击成本）
/// var (errorObj, contentType) = await ErrorObfuscationHelper.GenerateObfuscatedErrorResponseWithDelayAsync(500, 3000);
///
/// // 示例 8: 批量生成用于测试
/// List&lt;object&gt; errors = ErrorObfuscationHelper.GenerateBatchObfuscatedErrors(10);
///
/// // 示例 9: 使用配置选项
/// var options = ErrorObfuscationOptions.WithRandomDelay(100, 1000);
/// var (errorObj, contentType) = await ErrorObfuscationHelper.GenerateObfuscatedErrorResponseAsync(options);
/// </code>
/// <br/>
/// <br/>应用场景：
/// <list type="number">
/// <item>防止攻击者识别真实的技术栈</item>
/// <item>混淆自动化扫描工具</item>
/// <item>增加攻击者的时间成本</item>
/// <item>防止漏洞指纹识别</item>
/// <item>保护敏感错误信息不被泄露</item>
/// </list>
/// </remarks>
public static class ErrorObfuscationHelper
{
    /// <summary>
    /// 生成一个混淆的错误对象（随机格式）
    /// </summary>
    /// <returns>混淆的错误对象（可能是 Dictionary(JSON对象)、List(JSON数组)、string(纯文本/XML/HTML)）</returns>
    public static object GenerateObfuscatedError()
    {
        var random = Random.Shared;
        var formatType = random.Next(0, 5); // 0: JSON对象, 1: JSON数组, 2: 纯文本, 3: XML, 4: HTML

        return formatType switch
        {
            0 => GenerateJsonObject(),
            1 => GenerateJsonArray(),
            2 => GeneratePlainText(),
            3 => GenerateXml(),
            _ => GenerateHtml()
        };
    }

    /// <summary>
    /// 生成一个混淆的错误对象，可指定返回格式
    /// </summary>
    /// <param name="format">错误格式</param>
    /// <returns>指定格式的错误对象（Dictionary、List 或 string）</returns>
    public static object GenerateObfuscatedError(ErrorFormat format)
    {
        return format switch
        {
            ErrorFormat.JsonObject => GenerateJsonObject(),
            ErrorFormat.JsonArray => GenerateJsonArray(),
            ErrorFormat.PlainText => GeneratePlainText(),
            ErrorFormat.Xml => GenerateXml(),
            ErrorFormat.Html => GenerateHtml(),
            _ => GenerateObfuscatedError()
        };
    }

    /// <summary>
    /// 生成一个混淆的错误对象，可指定编程语言
    /// </summary>
    /// <param name="language">编程语言</param>
    /// <returns>JSON 字典对象</returns>
    public static Dictionary<string, object> GenerateObfuscatedError(ProgrammingLanguage language)
    {
        var error = ErrorUtilities.GetErrorByLanguage(language);
        return ConvertToJsonDictionary(JsonErrorGenerator.GenerateJsonObject(error));
    }

    /// <summary>
    /// 生成一个混淆的错误对象，可指定编程语言和输出格式
    /// </summary>
    /// <param name="language">编程语言</param>
    /// <param name="format">错误格式</param>
    /// <returns>指定格式的错误对象（Dictionary、List 或 string）</returns>
    public static object GenerateObfuscatedError(ProgrammingLanguage language, ErrorFormat format)
    {
        var error = ErrorUtilities.GetErrorByLanguage(language);
        return format switch
        {
            ErrorFormat.JsonObject => ConvertToJsonDictionary(JsonErrorGenerator.GenerateJsonObject(error)),
            ErrorFormat.JsonArray => ConvertToJsonArray(JsonErrorGenerator.GenerateJsonArray([error])),
            ErrorFormat.PlainText => PlainTextErrorGenerator.GeneratePlainText(error).ToString(),
            ErrorFormat.Xml => ErrorResponseSerializer.Serialize(XmlErrorGenerator.GenerateXml(error)),
            ErrorFormat.Html => ErrorResponseSerializer.Serialize(HtmlErrorGenerator.GenerateHtml(error)),
            _ => ConvertToJsonDictionary(JsonErrorGenerator.GenerateJsonObject(error))
        };
    }

    /// <summary>
    /// 生成错误响应（包含错误对象和内容类型）
    /// </summary>
    /// <returns>包含错误对象（Dictionary、List 或 string）和对应Content-Type的元组</returns>
    public static (object ErrorObject, string ContentType) GenerateObfuscatedErrorResponse()
    {
        var random = Random.Shared;
        var format = (ErrorFormat)random.Next(0, 5);
        var errorObj = GenerateObfuscatedError(format);
        var contentType = GetContentType(format);

        return (errorObj, contentType);
    }

    /// <summary>
    /// 生成错误响应（可指定格式）
    /// </summary>
    /// <param name="format">错误格式</param>
    /// <returns>包含错误对象和对应Content-Type的元组</returns>
    public static (object ErrorObject, string ContentType) GenerateObfuscatedErrorResponse(ErrorFormat format)
    {
        var errorObj = GenerateObfuscatedError(format);
        var contentType = GetContentType(format);

        return (errorObj, contentType);
    }

    /// <summary>
    /// 根据错误格式获取对应的Content-Type
    /// </summary>
    /// <param name="format">错误格式</param>
    /// <returns>Content-Type字符串</returns>
    public static string GetContentType(ErrorFormat format)
    {
        return format switch
        {
            ErrorFormat.JsonObject => "application/json; charset=utf-8",
            ErrorFormat.JsonArray => "application/json; charset=utf-8",
            ErrorFormat.PlainText => "text/plain; charset=utf-8",
            ErrorFormat.Xml => "application/xml; charset=utf-8",
            ErrorFormat.Html => "text/html; charset=utf-8",
            _ => "application/json; charset=utf-8"
        };
    }

    /// <summary>
    /// 生成带有随机延迟的错误响应（模拟服务器处理时间）
    /// </summary>
    /// <param name="minDelayMs">最小延迟毫秒数，默认100ms</param>
    /// <param name="maxDelayMs">最大延迟毫秒数，默认2000ms</param>
    /// <returns>错误响应的异步任务</returns>
    public static async Task<(object ErrorObject, string ContentType)> GenerateObfuscatedErrorResponseWithDelayAsync(
        int minDelayMs = 100,
        int maxDelayMs = 2000)
    {
        var random = Random.Shared;
        var delay = random.Next(minDelayMs, maxDelayMs);
        await Task.Delay(delay);

        return GenerateObfuscatedErrorResponse();
    }

    /// <summary>
    /// 批量生成混淆错误（用于测试或批量混淆）
    /// </summary>
    /// <param name="count">生成数量</param>
    /// <returns>错误对象列表</returns>
    public static List<object> GenerateBatchObfuscatedErrors(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than 0", nameof(count));
        }

        var errors = new List<object>(count);
        for (var i = 0; i < count; i++)
        {
            errors.Add(GenerateObfuscatedError());
        }

        return errors;
    }

    /// <summary>
    /// 根据配置选项生成混淆错误
    /// </summary>
    /// <param name="options">混淆配置选项</param>
    /// <returns>混淆的错误对象（Dictionary、List 或 string）</returns>
    public static object GenerateObfuscatedError(ErrorObfuscationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var error = options.Language.HasValue
            ? ErrorUtilities.GetErrorByLanguage(options.Language.Value)
            : ErrorUtilities.GetRandomError();

        if (options.Format.HasValue)
        {
            return options.Format.Value switch
            {
                ErrorFormat.JsonObject => ConvertToJsonDictionary(JsonErrorGenerator.GenerateJsonObject(error)),
                ErrorFormat.JsonArray => ConvertToJsonArray(JsonErrorGenerator.GenerateJsonArray([error])),
                ErrorFormat.PlainText => PlainTextErrorGenerator.GeneratePlainText(error).ToString(),
                ErrorFormat.Xml => ErrorResponseSerializer.Serialize(XmlErrorGenerator.GenerateXml(error)),
                ErrorFormat.Html => ErrorResponseSerializer.Serialize(HtmlErrorGenerator.GenerateHtml(error)),
                _ => ConvertToJsonDictionary(JsonErrorGenerator.GenerateJsonObject(error))
            };
        }

        return GenerateObfuscatedError();
    }

    /// <summary>
    /// 根据配置选项生成混淆错误响应（包含延迟）
    /// </summary>
    /// <param name="options">混淆配置选项</param>
    /// <returns>错误响应的异步任务</returns>
    public static async Task<(object ErrorObject, string ContentType)> GenerateObfuscatedErrorResponseAsync(
        ErrorObfuscationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DelayMs > 0)
        {
            await Task.Delay(options.DelayMs);
        }
        else if (options.RandomDelay)
        {
            var random = Random.Shared;
            var delay = random.Next(options.MinDelayMs, options.MaxDelayMs);
            await Task.Delay(delay);
        }

        var errorObj = GenerateObfuscatedError(options);
        var format = options.Format ?? (ErrorFormat)Random.Shared.Next(0, 5);
        var contentType = GetContentType(format);

        return (errorObj, contentType);
    }

    #region 私有方法

    /// <summary>
    /// 生成 JSON 对象（Dictionary）
    /// </summary>
    private static Dictionary<string, object> GenerateJsonObject()
    {
        var error = ErrorUtilities.GetRandomError();
        var response = JsonErrorGenerator.GenerateJsonObject(error);
        return ConvertToJsonDictionary(response);
    }

    /// <summary>
    /// 生成 JSON 数组（Dictionary）
    /// </summary>
    private static Dictionary<string, object> GenerateJsonArray()
    {
        var random = Random.Shared;
        var errorCount = random.Next(2, 5);
        var errors = new List<ErrorInfo>();
        for (var i = 0; i < errorCount; i++)
        {
            errors.Add(ErrorUtilities.GetRandomError());
        }

        var response = JsonErrorGenerator.GenerateJsonArray(errors);
        return ConvertToJsonArray(response);
    }

    /// <summary>
    /// 生成纯文本（string）
    /// </summary>
    private static string GeneratePlainText()
    {
        var error = ErrorUtilities.GetRandomError();
        var response = PlainTextErrorGenerator.GeneratePlainText(error);
        return response.ToString();
    }

    /// <summary>
    /// 生成 XML 字符串
    /// </summary>
    private static string GenerateXml()
    {
        var error = ErrorUtilities.GetRandomError();
        var response = XmlErrorGenerator.GenerateXml(error);
        return ErrorResponseSerializer.Serialize(response);
    }

    /// <summary>
    /// 生成 HTML 字符串
    /// </summary>
    private static string GenerateHtml()
    {
        var error = ErrorUtilities.GetRandomError();
        var response = HtmlErrorGenerator.GenerateHtml(error);
        return ErrorResponseSerializer.Serialize(response);
    }

    /// <summary>
    /// 将 JsonErrorResponse 转换为 Dictionary
    /// </summary>
    private static Dictionary<string, object> ConvertToJsonDictionary(JsonErrorResponse response)
    {
        return new Dictionary<string, object>
        {
            ["status"] = response.Status,
            ["error"] = response.Error,
            ["message"] = response.Message,
            ["exception"] = response.Exception,
            ["timestamp"] = response.Timestamp,
            ["timestampISO"] = response.TimestampISO,
            ["traceId"] = response.TraceId,
            ["requestId"] = response.RequestId,
            ["path"] = response.Path,
            ["method"] = response.Method,
            ["server"] = response.Server,
            ["database"] = response.Database,
            ["stackTrace"] = response.StackTrace,
            ["metadata"] = new Dictionary<string, object>
            {
                ["hostname"] = response.Metadata?.Hostname ?? string.Empty,
                ["pid"] = response.Metadata?.Pid ?? 0,
                ["threadId"] = response.Metadata?.ThreadId ?? 0,
                ["memoryUsage"] = response.Metadata?.MemoryUsage ?? string.Empty
            }
        };
    }

    /// <summary>
    /// 将 JsonErrorArrayResponse 转换为包含错误数组的 Dictionary
    /// </summary>
    private static Dictionary<string, object> ConvertToJsonArray(JsonErrorArrayResponse response)
    {
        return new Dictionary<string, object>
        {
            ["errors"] = response.Errors.Select(e => new Dictionary<string, object>
            {
                ["code"] = e.Code,
                ["type"] = e.Type,
                ["message"] = e.Message,
                ["detail"] = e.Detail,
                ["source"] = new Dictionary<string, object>
                {
                    ["exception"] = e.Source?.Exception ?? string.Empty
                }
            }).ToList(),
            ["timestamp"] = response.Timestamp,
            ["traceId"] = response.TraceId,
            ["count"] = response.Count
        };
    }

    #endregion
}
