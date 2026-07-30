// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Http.Configuration;

namespace XiHan.Framework.Http.Models;

/// <summary>
/// 代理验证结果
/// </summary>
public class ProxyValidationResult
{
    /// <summary>
    /// 代理配置
    /// </summary>
    public ProxyConfiguration Proxy { get; set; } = null!;

    /// <summary>
    /// 是否可用
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// 响应时间(毫秒)
    /// </summary>
    public long ResponseTimeMilliseconds { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 验证时间
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 返回的IP地址(如果可以获取)
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 返回的地理位置信息
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="proxy">代理配置</param>
    /// <param name="responseTime">响应时间</param>
    /// <returns></returns>
    public static ProxyValidationResult Success(ProxyConfiguration proxy, long responseTime)
    {
        return new ProxyValidationResult
        {
            Proxy = proxy,
            IsAvailable = true,
            ResponseTimeMilliseconds = responseTime
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="proxy">代理配置</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns></returns>
    public static ProxyValidationResult Failure(ProxyConfiguration proxy, string errorMessage)
    {
        return new ProxyValidationResult
        {
            Proxy = proxy,
            IsAvailable = false,
            ErrorMessage = errorMessage
        };
    }
}
