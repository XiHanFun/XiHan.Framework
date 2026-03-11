#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotContext
// Guid:06f1842b-ca12-4409-9904-d5463dc17f23
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:44:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Core;

/// <summary>
/// Bot 调度上下文
/// </summary>
public class BotContext
{
    private readonly List<BotDispatchResult> _results = [];
    private IReadOnlyList<IBotProvider> _providers = [];

    /// <summary>
    /// 创建上下文
    /// </summary>
    public BotContext(BotMessage message, IReadOnlyList<string> channels, CancellationToken cancellationToken)
    {
        Message = message;
        Channels = channels;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// 消息
    /// </summary>
    public BotMessage Message { get; }

    /// <summary>
    /// 请求的渠道列表
    /// </summary>
    public IReadOnlyList<string> Channels { get; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 上下文项
    /// </summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 选中的提供者列表
    /// </summary>
    public IReadOnlyList<IBotProvider> Providers => _providers;

    /// <summary>
    /// 调度结果列表
    /// </summary>
    public IReadOnlyList<BotDispatchResult> Results => _results;

    /// <summary>
    /// 策略名称
    /// </summary>
    public string? StrategyName { get; set; }

    /// <summary>
    /// 最后一次异常
    /// </summary>
    public Exception? LastException { get; set; }

    /// <summary>
    /// 是否跳过本次调度
    /// </summary>
    public bool IsSkipped { get; set; }

    /// <summary>
    /// 是否存在失败
    /// </summary>
    public bool HasFailures => _results.Any(result => !result.Result.IsSuccess);

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool IsSuccess => _results.Count > 0 && _results.All(result => result.Result.IsSuccess);

    /// <summary>
    /// 设置提供者列表
    /// </summary>
    public void SetProviders(IEnumerable<IBotProvider> providers)
    {
        _providers = providers?.ToArray() ?? [];
    }

    /// <summary>
    /// 添加提供者结果
    /// </summary>
    public void AddResult(string providerName, BotResult result)
    {
        _results.Add(new BotDispatchResult
        {
            ProviderName = providerName,
            Result = result
        });
    }

    /// <summary>
    /// 清空结果列表
    /// </summary>
    public void ClearResults()
    {
        _results.Clear();
    }
}
