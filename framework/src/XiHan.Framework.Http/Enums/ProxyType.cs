// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
