#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpServiceExtensions
// Guid:eb49c74d-450f-46da-ab34-be16755ac116
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:13:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Options;

namespace XiHan.Framework.Http.Extensions;

/// <summary>
/// HTTP 服务扩展方法
/// </summary>
public static class HttpServiceExtensions
{
    /// <summary>
    /// 添加授权头
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <param name="token">令牌</param>
    /// <param name="scheme">认证方案</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithAuthorization(this XiHanHttpRequestOptions options, string token, string scheme = "Bearer")
    {
        return options.AddHeader("Authorization", $"{scheme} {token}");
    }

    /// <summary>
    /// 添加基本认证
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithBasicAuth(this XiHanHttpRequestOptions options, string username, string password)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return options.AddHeader("Authorization", $"Basic {credentials}");
    }

    /// <summary>
    /// 设置内容类型为JSON
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions AsJson(this XiHanHttpRequestOptions options)
    {
        options.ContentType = "application/json";
        return options;
    }

    /// <summary>
    /// 设置内容类型为XML
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions AsXml(this XiHanHttpRequestOptions options)
    {
        options.ContentType = "application/xml";
        return options;
    }

    /// <summary>
    /// 设置内容类型为表单
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions AsForm(this XiHanHttpRequestOptions options)
    {
        options.ContentType = "application/x-www-form-urlencoded";
        return options;
    }

    /// <summary>
    /// 禁用重试
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithoutRetry(this XiHanHttpRequestOptions options)
    {
        options.EnableRetry = false;
        return options;
    }

    /// <summary>
    /// 禁用熔断器
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithoutCircuitBreaker(this XiHanHttpRequestOptions options)
    {
        options.EnableCircuitBreaker = false;
        return options;
    }

    /// <summary>
    /// 设置用户代理
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <param name="userAgent">用户代理</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithUserAgent(this XiHanHttpRequestOptions options, string userAgent)
    {
        return options.AddHeader("User-Agent", userAgent);
    }

    /// <summary>
    /// 添加关联唯一标识
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <param name="correlationId">关联唯一标识</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithCorrelationId(this XiHanHttpRequestOptions options, string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString();
        return options.AddHeader("X-Correlation-Id", correlationId);
    }

    /// <summary>
    /// 设置接受语言
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <param name="language">语言代码</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithLanguage(this XiHanHttpRequestOptions options, string language)
    {
        return options.AddHeader("Accept-Language", language);
    }

    /// <summary>
    /// 添加缓存控制
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <param name="cacheControl">缓存控制</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithCacheControl(this XiHanHttpRequestOptions options, string cacheControl)
    {
        return options.AddHeader("Cache-Control", cacheControl);
    }

    /// <summary>
    /// 禁用缓存
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithoutCache(this XiHanHttpRequestOptions options)
    {
        return options.WithCacheControl("no-cache, no-store, must-revalidate");
    }

    /// <summary>
    /// 使用指定的HTTP客户端
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <param name="clientName">客户端名称</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions UseClient(this XiHanHttpRequestOptions options, string clientName)
    {
        return options.AddTag("ClientName", clientName);
    }

    /// <summary>
    /// 启用详细日志
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithVerboseLogging(this XiHanHttpRequestOptions options)
    {
        options.LogRequest = true;
        options.LogResponse = true;
        return options;
    }

    /// <summary>
    /// 禁用日志
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    public static XiHanHttpRequestOptions WithoutLogging(this XiHanHttpRequestOptions options)
    {
        options.LogRequest = false;
        options.LogResponse = false;
        return options;
    }

    /// <summary>
    /// 获取成功的数据或抛出异常
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="result">HTTP结果</param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException">请求失败时抛出</exception>
    public static T GetDataOrThrow<T>(this HttpResult<T> result)
    {
        if (result.IsSuccess && result.Data != null)
        {
            return result.Data;
        }

        var message = result.ErrorMessage ?? "HTTP请求失败";
        if (result.Exception != null)
        {
            throw new HttpRequestException(message, result.Exception);
        }

        throw new HttpRequestException(message);
    }

    /// <summary>
    /// 获取成功的数据或返回默认值
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="result">HTTP结果</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns></returns>
    public static T GetDataOrDefault<T>(this HttpResult<T> result, T defaultValue = default!)
    {
        return result.IsSuccess && result.Data != null ? result.Data : defaultValue;
    }

    /// <summary>
    /// 检查是否为成功状态码
    /// </summary>
    /// <param name="result">HTTP结果</param>
    /// <returns></returns>
    public static bool IsSuccessStatusCode<T>(this HttpResult<T> result)
    {
        var statusCode = (int)result.StatusCode;
        return statusCode is >= 200 and <= 299;
    }

    /// <summary>
    /// 检查是否为客户端错误
    /// </summary>
    /// <param name="result">HTTP结果</param>
    /// <returns></returns>
    public static bool IsClientError<T>(this HttpResult<T> result)
    {
        var statusCode = (int)result.StatusCode;
        return statusCode is >= 400 and <= 499;
    }

    /// <summary>
    /// 检查是否为服务器错误
    /// </summary>
    /// <param name="result">HTTP结果</param>
    /// <returns></returns>
    public static bool IsServerError<T>(this HttpResult<T> result)
    {
        var statusCode = (int)result.StatusCode;
        return statusCode is >= 500 and <= 599;
    }

    /// <summary>
    /// 获取响应头值
    /// </summary>
    /// <param name="result">HTTP结果</param>
    /// <param name="headerName">头名称</param>
    /// <returns></returns>
    public static string? GetHeader<T>(this HttpResult<T> result, string headerName)
    {
        return result.Headers.TryGetValue(headerName, out var values) ? values.FirstOrDefault() : null;
    }

    /// <summary>
    /// 获取内容类型
    /// </summary>
    /// <param name="result">HTTP结果</param>
    /// <returns></returns>
    public static string? GetContentType<T>(this HttpResult<T> result)
    {
        return result.GetHeader("Content-Type");
    }

    /// <summary>
    /// 获取内容长度
    /// </summary>
    /// <param name="result">HTTP结果</param>
    /// <returns></returns>
    public static long? GetContentLength<T>(this HttpResult<T> result)
    {
        var contentLength = result.GetHeader("Content-Length");
        return long.TryParse(contentLength, out var length) ? length : null;
    }
}
