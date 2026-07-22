// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;

namespace XiHan.Framework.Utils.Net;

/// <summary>
/// IpExtensions
/// </summary>
public static class IpFormatExtensions
{
    /// <summary>
    /// IPAddress 转 String
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static string FormatIpToString(this IPAddress address)
    {
        return address.ToString();
    }

    /// <summary>
    /// byte[]转 String
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string FormatIpToString(this byte[] bytes)
    {
        return new IPAddress(bytes).ToString();
    }

    /// <summary>
    /// ip 转 ipV4
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static string FormatIpToV4String(this IPAddress address)
    {
        return address.MapToIPv4().ToString();
    }

    /// <summary>
    /// ip 转 ipV4
    /// </summary>
    /// <param name="ipStr"></param>
    /// <returns></returns>
    public static string FormatIpToV4String(this string ipStr)
    {
        return IPAddress.Parse(ipStr).MapToIPv4().ToString();
    }

    /// <summary>
    /// ip 转 ipV6
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static string FormatIpToV6String(this IPAddress address)
    {
        return address.MapToIPv6().ToString();
    }

    /// <summary>
    /// IPAddress 转 byte[]
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static byte[] FormatIpToByte(this IPAddress address)
    {
        return address.GetAddressBytes();
    }

    /// <summary>
    /// byte[]转 IPAddress
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static IPAddress FormatIpToAddress(this byte[] bytes)
    {
        return new IPAddress(bytes);
    }

    /// <summary>
    /// String 转 IPAddress
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static IPAddress FormatIpToAddress(this string str)
    {
        return IPAddress.Parse(str);
    }
}
