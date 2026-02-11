#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpResult
// Guid:8cec3fe1-ea25-49b3-bb54-1a839f0b0d3f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 20:16:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net;

namespace XiHan.Framework.Http.Models;

/// <summary>
/// HTTP 请求结果
/// </summary>
/// <typeparam name="T">响应数据类型</typeparam>
public class HttpResult<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// HTTP状态码
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 原始数据 string
    /// </summary>
    public string? RawDataString { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, IEnumerable<string>> Headers { get; set; } = [];

    /// <summary>
    /// 请求耗时(毫秒)
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 请求Url
    /// </summary>
    public string? RequestUrl { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? RequestMethod { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="data">响应数据</param>
    /// <param name="statusCode">状态码</param>
    /// <returns></returns>
    public static HttpResult<T> Success(T data, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResult<T>
        {
            IsSuccess = true,
            Data = data,
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="statusCode">状态码</param>
    /// <param name="exception">异常</param>
    /// <returns></returns>
    public static HttpResult<T> Failure(string errorMessage, HttpStatusCode statusCode = HttpStatusCode.InternalServerError, Exception? exception = null)
    {
        return new HttpResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            Exception = exception
        };
    }
}

/// <summary>
/// HTTP 请求结果(无泛型)
/// </summary>
public class HttpResult : HttpResult<object>
{
    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="statusCode">状态码</param>
    /// <returns></returns>
    public static HttpResult Success(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResult
        {
            IsSuccess = true,
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="statusCode">状态码</param>
    /// <param name="exception">异常</param>
    /// <returns></returns>
    public new static HttpResult Failure(string errorMessage, HttpStatusCode statusCode = HttpStatusCode.InternalServerError, Exception? exception = null)
    {
        return new HttpResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            Exception = exception
        };
    }
}
