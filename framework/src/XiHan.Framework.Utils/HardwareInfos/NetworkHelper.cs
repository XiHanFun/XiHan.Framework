#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NetworkHelper
// Guid:dc0502e1-f675-41d3-8a67-dbd590e76260
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-09 上午 01:11:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net.NetworkInformation;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.HardwareInfos;

/// <summary>
/// 网卡信息帮助类
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// 网卡信息
    /// </summary>
    public static List<NetworkInfo> NetworkInfos => GetNetworkInfos();

    /// <summary>
    /// 获取网卡信息
    /// </summary>
    /// <returns></returns>
    public static List<NetworkInfo> GetNetworkInfos()
    {
        List<NetworkInfo> networkInfos = [];

        try
        {
            // 获取可用网卡
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                .ToList();

            networkInfos.AddRange(from ni in interfaces
                                  let properties = ni.GetIPProperties()
                                  select new NetworkInfo()
                                  {
                                      Name = ni.Name,
                                      Description = ni.Description,
                                      Type = ni.NetworkInterfaceType.ToString(),
                                      Speed = ni.Speed.ToString("#,##0") + " bps",
                                      PhysicalAddress = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes()),
                                      DnsAddresses = properties.DnsAddresses.Select(ip => ip.ToString()).ToList(),
                                      IpAddresses = properties.UnicastAddresses.Select(ip => ip.Address + " / " + ip.IPv4Mask).ToList()
                                  });
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error("获取网卡信息出错，" + ex.Message);
        }

        return networkInfos;
    }
}

/// <summary>
/// 网卡信息
/// </summary>
public record NetworkInfo
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 速度
    /// </summary>
    public string Speed { get; set; } = string.Empty;

    /// <summary>
    /// 物理地址(mac地址)
    /// </summary>
    public string PhysicalAddress { get; set; } = string.Empty;

    /// <summary>
    /// DNS地址
    /// </summary>
    public List<string> DnsAddresses { get; set; } = [];

    /// <summary>
    /// IP地址
    /// </summary>
    public List<string> IpAddresses { get; set; } = [];
}