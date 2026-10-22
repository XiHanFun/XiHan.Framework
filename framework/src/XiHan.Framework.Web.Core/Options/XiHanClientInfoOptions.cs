// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Core.Options;

/// <summary>
/// 客户端信息解析配置
/// </summary>
public class XiHanClientInfoOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Core:ClientInfo";

    /// <summary>
    /// 是否启用 IP 地理位置解析
    /// </summary>
    public bool EnableIpRegion { get; set; } = true;

    /// <summary>
    /// IPv4 的 ip2region xdb 文件路径（支持相对路径，按内容根解析）
    /// </summary>
    public string? Ip2RegionDbPath { get; set; } = "IpDatabases/ip2region.xdb";

    /// <summary>
    /// IPv6 的 ip2region xdb 文件路径（支持相对路径，按内容根解析）；文件不存在时 IPv6 地址不解析归属地
    /// </summary>
    public string? Ip2RegionV6DbPath { get; set; } = "IpDatabases/ip2region_v6.xdb";
}
