// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Sockets;

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// 地址范围判断：回环与内网地址不查归属地
/// </summary>
internal static class IpAddressScope
{
    /// <summary>
    /// 把 IPv4 映射的 IPv6 地址还原成 IPv4
    /// </summary>
    public static IPAddress Normalize(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    /// <summary>
    /// 是否回环地址
    /// </summary>
    public static bool IsLoopback(IPAddress address)
    {
        return IPAddress.IsLoopback(Normalize(address));
    }

    /// <summary>
    /// 是否内网地址（IPv4 私有网段，IPv6 链路本地、站点本地与组播）
    /// </summary>
    public static bool IsPrivate(IPAddress address)
    {
        var normalized = Normalize(address);
        if (normalized.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return normalized.IsIPv6LinkLocal || normalized.IsIPv6SiteLocal || normalized.IsIPv6Multicast;
        }

        var bytes = normalized.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
