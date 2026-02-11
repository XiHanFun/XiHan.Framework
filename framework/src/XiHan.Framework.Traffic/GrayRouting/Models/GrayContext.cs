#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GrayContext
// Guid:5e6f7a8b-9c0d-1e2f-3a4b-5c6d7e8f9a0b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Traffic.GrayRouting.Models;

/// <summary>
/// 灰度上下文
/// </summary>
/// <remarks>
/// 封装进行灰度判断所需的所有上下文信息
/// </remarks>
public class GrayContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public GrayContext()
    {
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ExtensionData = new Dictionary<string, object>();
    }

    /// <summary>
    /// 用户ID
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 租户ID
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? RequestMethod { get; set; }

    /// <summary>
    /// 客户端IP地址
    /// </summary>
    public string? ClientIpAddress { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public IDictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    public IDictionary<string, object>? ExtensionData { get; set; }
}
