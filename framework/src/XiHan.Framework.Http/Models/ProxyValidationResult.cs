#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyValidationResult
// Guid:ac35a1a1-1485-43e6-92fb-3166d1197990
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
