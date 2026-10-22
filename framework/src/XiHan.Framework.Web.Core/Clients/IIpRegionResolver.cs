// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// IP 归属地解析
/// </summary>
/// <remarks>
/// 登录地址、会话记录与访问策略的地区条件共用一份离线库，进程内只加载一次。实现须线程安全。
/// </remarks>
public interface IIpRegionResolver
{
    /// <summary>
    /// 解析归属地
    /// </summary>
    /// <param name="address">IP 地址（IPv4 映射的 IPv6 地址按 IPv4 查）</param>
    /// <returns>归属地；未启用、库不可用、回环与内网地址、查不到时为 <see langword="null"/></returns>
    IpRegion? Resolve(IPAddress address);
}
