#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpRequestOptions
// Guid:7ef80d65-9d32-44ed-82a6-c8d2fe5730a2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:17:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Http.Options;

/// <summary>
/// HTTP 请求选项
/// </summary>
public class HttpRequestOptions
{
    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// 查询参数
    /// </summary>
    public Dictionary<string, string> QueryParameters { get; set; } = [];

    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool? EnableRetry { get; set; }

    /// <summary>
    /// 是否启用熔断器
    /// </summary>
    public bool? EnableCircuitBreaker { get; set; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// 字符编码
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// 是否验证SSL证书
    /// </summary>
    public bool? ValidateSslCertificate { get; set; }

    /// <summary>
    /// 请求标识(用于日志追踪)
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// 自定义标签(用于监控和日志)
    /// </summary>
    public Dictionary<string, object> Tags { get; set; } = [];

    /// <summary>
    /// 是否记录请求日志
    /// </summary>
    public bool? LogRequest { get; set; }

    /// <summary>
    /// 是否记录响应日志
    /// </summary>
    public bool? LogResponse { get; set; }

    /// <summary>
    /// 添加请求头
    /// </summary>
    /// <param name="name">头名称</param>
    /// <param name="value">头值</param>
    /// <returns></returns>
    public HttpRequestOptions AddHeader(string name, string value)
    {
        Headers[name] = value;
        return this;
    }

    /// <summary>
    /// 添加查询参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <returns></returns>
    public HttpRequestOptions AddQueryParameter(string name, string value)
    {
        QueryParameters[name] = value;
        return this;
    }

    /// <summary>
    /// 添加标签
    /// </summary>
    /// <param name="key">标签键</param>
    /// <param name="value">标签值</param>
    /// <returns></returns>
    public HttpRequestOptions AddTag(string key, object value)
    {
        Tags[key] = value;
        return this;
    }

    /// <summary>
    /// 设置超时时间
    /// </summary>
    /// <param name="timeout">超时时间</param>
    /// <returns></returns>
    public HttpRequestOptions SetTimeout(TimeSpan timeout)
    {
        Timeout = timeout;
        return this;
    }

    /// <summary>
    /// 设置请求标识
    /// </summary>
    /// <param name="requestId">请求标识</param>
    /// <returns></returns>
    public HttpRequestOptions SetRequestId(string requestId)
    {
        RequestId = requestId;
        return this;
    }

    /// <summary>
    /// 构建查询字符串
    /// </summary>
    /// <returns></returns>
    public string BuildQueryString()
    {
        if (QueryParameters.Count == 0)
        {
            return string.Empty;
        }

        var queryString = string.Join("&",
            QueryParameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"?{queryString}";
    }
}
