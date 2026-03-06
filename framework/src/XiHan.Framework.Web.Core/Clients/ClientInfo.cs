#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ClientInfo
// Guid:5cc7f9cc-11b2-4e57-bf8c-34fd22a8f0bf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 14:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// 客户端信息
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// 客户端IP
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 地理位置
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 原始UA
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 浏览器
    /// </summary>
    public string? Browser { get; set; }

    /// <summary>
    /// 操作系统
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// 设备名称
    /// </summary>
    public string? DeviceName { get; set; }
}
