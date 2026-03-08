#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebApiLogQueueOptions
// Guid:9bf6f17f-9df3-4d35-9b6c-4c8b97f4214a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Options;

/// <summary>
/// WebApi 异步日志队列选项
/// </summary>
public class XiHanWebApiLogQueueOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Api:LogQueue";

    /// <summary>
    /// 是否启用访问日志队列
    /// </summary>
    public bool EnableAccessLogQueue { get; set; } = false;

    /// <summary>
    /// 是否启用操作日志队列
    /// </summary>
    public bool EnableOperationLogQueue { get; set; } = false;

    /// <summary>
    /// 是否启用异常日志队列
    /// </summary>
    public bool EnableExceptionLogQueue { get; set; } = false;

    /// <summary>
    /// 队列容量
    /// </summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>
    /// 是否在队列满时丢弃
    /// </summary>
    public bool DropOnFull { get; set; } = false;

    /// <summary>
    /// 批处理大小
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// 批处理间隔（毫秒）
    /// </summary>
    public int BatchDelayMilliseconds { get; set; } = 200;
}
