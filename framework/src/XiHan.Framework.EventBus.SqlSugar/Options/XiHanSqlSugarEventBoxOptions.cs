// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.SqlSugar.Options;

/// <summary>
/// 事件收发件箱 SqlSugar 存储配置
/// </summary>
public class XiHanSqlSugarEventBoxOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:EventBus:SqlSugar";

    /// <summary>
    /// 领取超时，超过该时长仍未完结的已领取记录可被重新领取，收发件箱共用
    /// </summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 收件箱已处理与已丢弃记录的保留期，超过该时长的记录被清理
    /// </summary>
    public TimeSpan InboxRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
