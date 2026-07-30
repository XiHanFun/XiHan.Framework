// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Bot.Telegram.MultiBot;

/// <summary>
/// 机器人实例注册表（由管理器维护；热路径使用无锁 TryGet，无任何 I/O）
/// </summary>
public sealed class BotRegistry
{
    /// <summary>
    /// 旧实例延迟释放宽限期（秒）：须大于 HttpClient 超时上限（TimeoutSeconds 默认 100 秒），
    /// 覆盖在途 Webhook/轮询分发以及 Notifier 重试环持有旧实例的整个窗口
    /// </summary>
    private const int DisposeGraceSeconds = 150;

    private readonly ConcurrentDictionary<string, BotInstance> _bots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 当前已注册机器人数量
    /// </summary>
    public int Count => _bots.Count;

    /// <summary>
    /// 添加或更新机器人实例（被替换的旧实例延迟释放，避免打断在途请求）
    /// </summary>
    /// <param name="bot">机器人实例</param>
    public void AddOrUpdate(BotInstance bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        BotInstance? replaced = null;
        _ = _bots.AddOrUpdate(bot.Name, bot, (_, existing) =>
        {
            if (!ReferenceEquals(existing, bot))
            {
                replaced = existing;
            }

            return bot;
        });

        if (replaced is not null)
        {
            DisposeLater(replaced);
        }
    }

    /// <summary>
    /// 尝试获取机器人实例（无锁，热路径安全）
    /// </summary>
    /// <param name="name">机器人名称</param>
    /// <param name="bot">机器人实例</param>
    /// <returns>是否找到</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out BotInstance? bot)
    {
        bot = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _bots.TryGetValue(name.Trim(), out bot);
    }

    /// <summary>
    /// 获取机器人实例（未注册或未运行时抛异常）
    /// </summary>
    /// <param name="name">机器人名称</param>
    /// <returns>机器人实例</returns>
    /// <exception cref="KeyNotFoundException">机器人未注册或未运行</exception>
    public BotInstance GetRequired(string name)
    {
        if (!TryGet(name, out var bot))
        {
            throw new KeyNotFoundException($"未找到机器人：{name}（未配置或未运行）。");
        }

        return bot;
    }

    /// <summary>
    /// 获取所有机器人实例
    /// </summary>
    /// <returns>机器人实例列表</returns>
    public IReadOnlyList<BotInstance> GetAll()
    {
        return [.. _bots.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// 移除机器人实例（移除后延迟释放，避免打断在途请求）
    /// </summary>
    /// <param name="name">机器人名称</param>
    /// <returns>是否移除成功</returns>
    public bool Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (_bots.TryRemove(name.Trim(), out var removed))
        {
            DisposeLater(removed);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 宽限期后释放旧实例：立即 Dispose 会取消旧 HttpClient 上的在途请求
    /// （如 Notifier 重试环、后台分发中的回复发送），导致消息在新实例明明可用时最终失败
    /// </summary>
    /// <param name="bot">被替换/移除的旧实例</param>
    private static void DisposeLater(BotInstance bot)
    {
        _ = Task.Delay(TimeSpan.FromSeconds(DisposeGraceSeconds)).ContinueWith(
            _ => bot.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
