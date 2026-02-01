#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyConfiguration
// Guid:e85f4133-7896-4dc8-a898-44afa1f3d06d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Http.Enums;

namespace XiHan.Framework.Http.Configuration;

/// <summary>
/// 代理配置
/// </summary>
public class ProxyConfiguration
{
    /// <summary>
    /// 代理地址 (例如: 127.0.0.1)
    /// </summary>
    [Required]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 代理端口
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; }

    /// <summary>
    /// 代理类型
    /// </summary>
    public ProxyType Type { get; set; } = ProxyType.Http;

    /// <summary>
    /// 用户名 (用于需要认证的代理)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码 (用于需要认证的代理)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 代理名称/标识
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 优先级 (数字越小优先级越高)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 最大并发连接数
    /// </summary>
    [Range(1, 1000)]
    public int MaxConcurrentConnections { get; set; } = 10;

    /// <summary>
    /// 超时时间(秒)
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 绕过的地址列表 (例如: localhost, 127.0.0.1)
    /// </summary>
    public List<string> BypassList { get; set; } = [];

    /// <summary>
    /// 是否对本地地址使用代理
    /// </summary>
    public bool UseProxyForLocalAddress { get; set; } = false;

    /// <summary>
    /// 自定义标签
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = [];

    /// <summary>
    /// 获取完整的代理地址
    /// </summary>
    /// <returns></returns>
    public string GetProxyAddress()
    {
        var scheme = Type switch
        {
            ProxyType.Http => "http",
            ProxyType.Https => "https",
            ProxyType.Socks4 => "socks4",
            ProxyType.Socks4A => "socks4a",
            ProxyType.Socks5 => "socks5",
            _ => "http"
        };

        if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
        {
            return $"{scheme}://{Uri.EscapeDataString(Username)}:{Uri.EscapeDataString(Password)}@{Host}:{Port}";
        }

        return $"{scheme}://{Host}:{Port}";
    }

    /// <summary>
    /// 获取 Uri 格式的代理地址
    /// </summary>
    /// <returns></returns>
    public Uri GetProxyUri()
    {
        return new Uri(GetProxyAddress());
    }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    /// <returns></returns>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            return false;
        }

        if (Port < 1 || Port > 65535)
        {
            return false;
        }

        return true;
    }
}
