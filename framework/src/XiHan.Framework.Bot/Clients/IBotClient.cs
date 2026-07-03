#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotClient
// Guid:ba79409a-7034-44e2-bee3-60a6aa47ce95
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:43:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Clients;

/// <summary>
/// Bot 客户端入口
/// </summary>
/// <remarks>
/// 所有发送方法返回 <see cref="BotDispatchResult"/>（整体成败 + 各提供者明细），
/// 调用方据此实现 fail-closed 判定；无提供者/被管道跳过均体现为 IsSuccess=false。
/// 取消令牌贯穿全链（管道等待/策略循环/提供者底层调用），渠道列表为空或 null 表示广播全部提供者。
/// </remarks>
public interface IBotClient
{
    /// <summary>
    /// 向所有提供者发送消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度聚合结果</returns>
    Task<BotDispatchResult> SendAsync(BotMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向指定渠道/提供者发送消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="channels">渠道/提供者名列表（空或 null 表示广播）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度聚合结果</returns>
    Task<BotDispatchResult> SendAsync(BotMessage message, IReadOnlyList<string>? channels, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按模板名称发送
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="model">模板模型</param>
    /// <param name="channels">渠道/提供者名列表（空或 null 表示广播）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度聚合结果</returns>
    Task<BotDispatchResult> SendTemplateAsync(string templateName, object? model = null, IReadOnlyList<string>? channels = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量发送消息（逐条发送，返回逐条聚合结果）
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="channels">渠道/提供者名列表（空或 null 表示广播）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>逐条调度聚合结果</returns>
    Task<IReadOnlyList<BotDispatchResult>> SendBatchAsync(IEnumerable<BotMessage> messages, IReadOnlyList<string>? channels = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 延迟发送消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="delay">延迟时长</param>
    /// <param name="channels">渠道/提供者名列表（空或 null 表示广播）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度聚合结果</returns>
    Task<BotDispatchResult> SendDelayedAsync(BotMessage message, TimeSpan delay, IReadOnlyList<string>? channels = null, CancellationToken cancellationToken = default);
}
