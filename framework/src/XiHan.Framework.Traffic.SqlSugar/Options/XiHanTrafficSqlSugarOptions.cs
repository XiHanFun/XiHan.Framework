// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Traffic.SqlSugar.Options;

/// <summary>
/// 流量治理 SqlSugar 持久化配置
/// </summary>
public class XiHanTrafficSqlSugarOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Traffic:SqlSugar";

    /// <summary>
    /// 缓存刷新间隔
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);
}
