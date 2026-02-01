#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IpFormatExtensions
// Guid:90ce7ed9-3669-4eb7-a295-33155633dd77
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 05:28:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
