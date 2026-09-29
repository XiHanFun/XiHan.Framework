// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.SqlSugar.Options;

/// <summary>
/// 认证存储 SqlSugar 配置
/// </summary>
public class XiHanAuthenticationSqlSugarOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Authentication:SqlSugar";

    /// <summary>
    /// 是否启用刷新令牌重用检测
    /// </summary>
    /// <remarks>
    /// 启用时，已撤销的刷新令牌再次被校验会撤销同一租户下同一主体的全部未撤销刷新令牌。
    /// </remarks>
    public bool RefreshTokenReuseDetection { get; set; } = true;

    /// <summary>
    /// 刷新令牌重用检测的宽限期
    /// </summary>
    /// <remarks>
    /// 令牌撤销后在此时长内再次被校验，只拒绝、不级联撤销。为零时任何再次校验都级联撤销。
    /// </remarks>
    public TimeSpan RefreshTokenReuseGracePeriod { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 每保存多少次刷新令牌清理一次已过期的记录，小于等于 0 时不清理
    /// </summary>
    public int RefreshTokenCleanupFrequency { get; set; } = 256;
}
