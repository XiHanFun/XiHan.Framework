// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Core.Access;

/// <summary>
/// 请求访问判定
/// </summary>
public sealed class RequestAccessDecision
{
    private RequestAccessDecision(bool isAllowed, int statusCode, string? message, object? data, IReadOnlyDictionary<string, string> headers)
    {
        IsAllowed = isAllowed;
        StatusCode = statusCode;
        Message = message;
        Data = data;
        Headers = headers;
    }

    /// <summary>
    /// 放行
    /// </summary>
    public static RequestAccessDecision Allow { get; } = new(true, 0, null, null, new Dictionary<string, string>());

    /// <summary>
    /// 是否放行
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// 拒绝时的 HTTP 状态码（同时作为响应信封的业务码）
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// 拒绝时给调用方看的说明
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// 拒绝时随信封返回的数据
    /// </summary>
    public object? Data { get; }

    /// <summary>
    /// 拒绝时附加的响应头（如 Retry-After）
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// 拒绝
    /// </summary>
    /// <param name="statusCode">HTTP 状态码，须为 4xx 或 5xx</param>
    /// <param name="message">说明</param>
    /// <param name="data">随信封返回的数据</param>
    /// <param name="headers">附加的响应头</param>
    /// <returns>判定结果</returns>
    /// <exception cref="ArgumentOutOfRangeException">状态码不是 4xx 或 5xx</exception>
    public static RequestAccessDecision Deny(int statusCode, string message, object? data = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (statusCode is < 400 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "拒绝的状态码须为 4xx 或 5xx");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new RequestAccessDecision(false, statusCode, message, data, headers ?? new Dictionary<string, string>());
    }
}
