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
    /// IP2Region xdb 文件路径（支持相对路径）
    /// </summary>
    public string? Ip2RegionDbPath { get; set; } = "IpDatabases/ip2region.xdb";
}
