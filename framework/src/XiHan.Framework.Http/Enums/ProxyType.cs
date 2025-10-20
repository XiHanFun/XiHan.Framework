#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyType
// Guid:d3c5e6f7-8a9b-4c5d-9e2f-1a3b4c5d6e7f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Http.Enums;

/// <summary>
/// 代理类型枚举
/// </summary>
public enum ProxyType
{
    /// <summary>
    /// HTTP 代理
    /// </summary>
    Http = 0,

    /// <summary>
    /// HTTPS 代理
    /// </summary>
    Https = 1,

    /// <summary>
    /// SOCKS4 代理
    /// </summary>
    Socks4 = 2,

    /// <summary>
    /// SOCKS4A 代理
    /// </summary>
    Socks4A = 3,

    /// <summary>
    /// SOCKS5 代理
    /// </summary>
    Socks5 = 4
}

