#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanRedisCacheOptions
// Guid:a1b2c3d4-e5f6-7890-1234-567890abcdef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Options;

/// <summary>
/// 曦寒 Redis 缓存配置选项
/// </summary>
public class XiHanRedisCacheOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Caching:Redis";

    /// <summary>
    /// 是否启用 Redis 缓存
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Redis 连接配置字符串
    /// </summary>
    /// <remarks>
    /// 示例: "127.0.0.1:6379,password=mypassword,defaultDatabase=0"
    /// </remarks>
    public string Configuration { get; set; } = string.Empty;

    /// <summary>
    /// 实例名称（用于键前缀）
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// 同步超时时间（毫秒）
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// 异步超时时间（毫秒）
    /// </summary>
    public int AsyncTimeout { get; set; } = 5000;

    /// <summary>
    /// 是否允许管理员操作
    /// </summary>
    public bool AllowAdmin { get; set; } = false;

    /// <summary>
    /// 是否使用 SSL
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// 是否中止连接失败
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;
}
