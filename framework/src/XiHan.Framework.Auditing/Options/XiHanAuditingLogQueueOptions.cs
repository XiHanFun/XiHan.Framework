// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Options;

/// <summary>
/// WebApi 异步日志队列选项
/// </summary>
public class XiHanAuditingLogQueueOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Auditing:LogQueue";

    /// <summary>
    /// 不记录任何请求日志（访问日志 + 请求起止日志）的路径前缀（不区分大小写）。
    /// </summary>
    /// <remarks>
    /// 默认忽略 SignalR 的 <c>/hubs</c>（协商/长轮询/心跳高频且无审计价值）。需要忽略其它前缀时在配置中追加。
    /// </remarks>
    public string[] IgnoredPathPrefixes { get; set; } = ["/hubs"];

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
    /// 是否启用接口日志队列
    /// </summary>
    public bool EnableApiLogQueue { get; set; } = false;

    /// <summary>
    /// 是否启用登录日志队列
    /// </summary>
    public bool EnableLoginLogQueue { get; set; } = false;

    /// <summary>
    /// 队列容量
    /// </summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>
    /// 是否在队列满时丢弃
    /// </summary>
    /// <remarks>
    /// <c>true</c>：队列满时丢弃当前日志并记一条警告，不阻塞调用方；
    /// <c>false</c>（默认）：队列满时等待空位，反压到调用方（请求线程）。
    /// </remarks>
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
