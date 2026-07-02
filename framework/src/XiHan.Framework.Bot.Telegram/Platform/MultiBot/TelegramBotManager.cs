#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotManager
// Guid:ba1f8099-9f7e-4b8b-8803-068809881e63
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XiHan.Framework.Bot.Telegram.Platform.Abstractions;
using XiHan.Framework.Bot.Telegram.Platform.Core;
using XiHan.Framework.Bot.Telegram.Platform.Options;
using XiHan.Framework.Bot.Telegram.Platform.Routing;

namespace XiHan.Framework.Bot.Telegram.Platform.MultiBot;

/// <summary>
/// Telegram 机器人管理器（统一启动多个机器人，双模传输 + 动态刷新）
/// </summary>
/// <remarks>
/// 传输模式：WebhookBaseUrl 非空 → SetWebhook（含 secret_token）；为空 → DeleteWebhook + 长轮询。
/// 后台刷新循环按周期 diff 配置：新增启动 / 变更重启 / 删除停止；平台未启用（Enabled=false）时空转不拉起任何机器人。
/// </remarks>
public sealed class TelegramBotManager
{
    private const int MaxDispatchConcurrency = 16;

    /// <summary>
    /// 启动初始化失败时的刷新循环兜底间隔秒数（设置存储不可用时以该周期重试拉取设置与配置）
    /// </summary>
    private const int InitRetryFallbackSeconds = 30;

    private readonly BotRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramBotHandlerCatalog _catalog;
    private readonly TelegramUpdateDispatcher _dispatcher;
    private readonly ILogger<TelegramBotManager> _logger;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _dispatchThrottle = new(MaxDispatchConcurrency, MaxDispatchConcurrency);
    private readonly ConcurrentDictionary<string, BotInstance> _runningBots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pollingBotTokens = new(StringComparer.OrdinalIgnoreCase);

    private TimeSpan _refreshInterval = TimeSpan.Zero;
    private volatile bool _enabled;
    private string _webhookBaseUrl = string.Empty;
    private string _webhookRoutePrefix = TelegramBotPlatformConsts.DefaultWebhookRoutePrefix;
    private string _webhookSecretToken = string.Empty;
    private TelegramBotNetworkOptions _networkOptions = new();
    private Task? _refreshLoopTask;
    private CancellationTokenSource? _refreshLoopCts;
    private CancellationTokenSource? _runningCts;
    private volatile bool _started;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="registry">机器人注册表</param>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="catalog">处理器目录（用于同步命令菜单）</param>
    /// <param name="dispatcher">Update 分发器</param>
    /// <param name="logger">日志记录器</param>
    public TelegramBotManager(
        BotRegistry registry,
        IServiceScopeFactory scopeFactory,
        TelegramBotHandlerCatalog catalog,
        TelegramUpdateDispatcher dispatcher,
        ILogger<TelegramBotManager> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 当前是否已启动
    /// </summary>
    public bool IsStarted => _started;

    /// <summary>
    /// 平台是否启用（由设置存储实时刷新）
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// 当前生效的 Webhook 路由前缀
    /// </summary>
    public string WebhookRoutePrefix => _webhookRoutePrefix;

    /// <summary>
    /// 当前生效的 Webhook 密钥令牌（Webhook 模式强制要求非空：为空时拒绝注册 Webhook，中间件拒绝所有 Webhook 请求）
    /// </summary>
    public string WebhookSecretToken => _webhookSecretToken;

    private bool UseWebhook => !string.IsNullOrWhiteSpace(_webhookBaseUrl);

    /// <summary>
    /// 获取当前管理器及所有机器人的运行状态
    /// </summary>
    /// <returns>运行状态</returns>
    public BotManagerStatus GetStatus()
    {
        var mode = UseWebhook ? "webhook" : "polling";
        return new BotManagerStatus
        {
            IsStarted = _started,
            Enabled = _enabled,
            TransportMode = mode,
            TotalBots = _runningBots.Count,
            Bots = [.. _runningBots.Values.Select(b => new BotRunningInfo
            {
                Name = b.Name,
                Mode = mode,
                Username = b.Username,
                BotId = b.BotId,
                // 轮询模式按真实接收器状态上报（409 冲突自停后为 false），避免状态误报掩盖故障
                IsRunning = UseWebhook || _pollingBotTokens.ContainsKey(b.Name)
            })]
        };
    }

    /// <summary>
    /// 立即刷新机器人配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            await StartAsync(cancellationToken);
            return;
        }

        await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// 启动管理器（读取设置并按当前传输模式拉起机器人；未启用时仅启动刷新循环空转）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return;
            }

            // 管理器整体生命周期令牌独立创建，不链调用方令牌：
            // 调用方令牌（宿主启动 / 管理端请求）只作用于启动过程本身，请求中止不得连带停掉全部机器人
            _runningCts = new CancellationTokenSource();
            using var initCts = CancellationTokenSource.CreateLinkedTokenSource(_runningCts.Token, cancellationToken);
            try
            {
                _ = await UpdateSettingsAsync(initCts.Token, detectChanges: false);
                await RefreshAsync(initCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 初始化失败不阻断启动：仍建立刷新循环，由后续刷新周期重试拉取设置与配置
                if (_refreshInterval <= TimeSpan.Zero)
                {
                    _refreshInterval = TimeSpan.FromSeconds(InitRetryFallbackSeconds);
                }

                _logger.LogError(ex,
                    "Telegram Bot 管理器初始化失败，将由刷新循环按周期重试。RetrySeconds={RetrySeconds}",
                    _refreshInterval.TotalSeconds);
            }

            _started = true;
            ResetRefreshLoop();
            _logger.LogInformation(
                "Telegram Bot 管理器已启动。Enabled={Enabled}, TotalBots={TotalBots}", _enabled, _runningBots.Count);
        }
        finally
        {
            _ = _startLock.Release();
        }
    }

    /// <summary>
    /// 停止管理器与全部机器人接收
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // 与 StartAsync 共用启动锁，避免启动/停止竞态下对 _started/_runningCts 的交错读写
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (!_started)
            {
                return;
            }

            await StopRefreshLoopAsync();

            _runningCts?.Cancel();
            _runningCts?.Dispose();
            _runningCts = null;
            await StopAllBotsAsync(deleteWebhook: false, cancellationToken);
            _started = false;

            _logger.LogInformation("Telegram Bot 管理器已停止。");
        }
        finally
        {
            _ = _startLock.Release();
        }
    }

    /// <summary>
    /// 分发一条 Update（内联等待处理完成；并发受闸门限制）
    /// </summary>
    /// <param name="bot">机器人实例</param>
    /// <param name="update">Telegram Update</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DispatchAsync(BotInstance bot, Update update, CancellationToken cancellationToken = default)
    {
        await _dispatchThrottle.WaitAsync(cancellationToken);
        try
        {
            await _dispatcher.DispatchAsync(bot, update, cancellationToken);
        }
        finally
        {
            _ = _dispatchThrottle.Release();
        }
    }

    /// <summary>
    /// 将一条 Update 排入后台分发（Webhook 场景：入队后立即返回，处理生命周期与 HTTP 请求解耦）
    /// </summary>
    /// <remarks>
    /// 取消令牌使用管理器自身的运行令牌（仅应用关停才取消），慢处理器不再被客户端断连半途取消；
    /// 并发同样受闸门限制。
    /// </remarks>
    /// <param name="bot">机器人实例</param>
    /// <param name="update">Telegram Update</param>
    public void QueueDispatch(BotInstance bot, Update update)
    {
        try
        {
            var runningCts = _runningCts;
            if (runningCts is null || runningCts.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Telegram Update 入队跳过（管理器未运行）。Bot={BotName}, UpdateId={UpdateId}", bot.Name, update.Id);
                return;
            }

            _ = DispatchInBackgroundAsync(bot, update, runningCts.Token);
        }
        catch (ObjectDisposedException)
        {
            // 管理器正在关闭
        }
    }

    private static string NormalizeRoutePrefix(string? value)
    {
        var path = string.IsNullOrWhiteSpace(value) ? TelegramBotPlatformConsts.DefaultWebhookRoutePrefix : value.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return path.TrimEnd('/');
    }

    private void ResetRefreshLoop()
    {
        CancelRefreshLoop();

        if (!_started || _runningCts is null || _runningCts.IsCancellationRequested || _refreshInterval <= TimeSpan.Zero)
        {
            _refreshLoopTask = null;
            return;
        }

        var loopCts = CancellationTokenSource.CreateLinkedTokenSource(_runningCts.Token);
        _refreshLoopCts = loopCts;
        _refreshLoopTask = Task.Run(() => RunRefreshLoopAsync(loopCts), CancellationToken.None);
    }

    private void CancelRefreshLoop()
    {
        var loopCts = Interlocked.Exchange(ref _refreshLoopCts, null);
        if (loopCts is null)
        {
            return;
        }

        try
        {
            loopCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task StopRefreshLoopAsync()
    {
        var refreshTask = _refreshLoopTask;
        CancelRefreshLoop();
        _refreshLoopTask = null;

        if (refreshTask is null)
        {
            return;
        }

        try
        {
            await refreshTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunRefreshLoopAsync(CancellationTokenSource loopCts)
    {
        try
        {
            var cancellationToken = loopCts.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                var interval = _refreshInterval;
                if (interval <= TimeSpan.Zero)
                {
                    return;
                }

                await Task.Delay(interval, cancellationToken);

                try
                {
                    await RefreshAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Telegram Bot 刷新循环失败。");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            loopCts.Dispose();
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var transportChanged = await UpdateSettingsAsync(cancellationToken, detectChanges: true);

            // 平台未启用：停掉全部机器人并空转（保留刷新循环，启用后自动拉起）
            if (!_enabled)
            {
                if (!_runningBots.IsEmpty)
                {
                    await StopAllBotsAsync(deleteWebhook: false, cancellationToken);
                    _logger.LogInformation("Telegram Bot 平台已停用，全部机器人已停止。");
                }

                return;
            }

            var configs = await LoadConfigsAsync(cancellationToken);
            var configMap = configs.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

            if (transportChanged)
            {
                foreach (var item in _runningBots.ToList())
                {
                    await RemoveBotAsync(item.Key, item.Value, deleteWebhook: false, cancellationToken);
                }
            }

            foreach (var config in configs)
            {
                if (_runningBots.TryGetValue(config.Name, out var running))
                {
                    // 轮询模式对账：已注册但接收器已停（如 409 冲突自停）的机器人需要重启恢复
                    var pollingDead = !UseWebhook && !_pollingBotTokens.ContainsKey(config.Name);
                    if (!running.Config.IsSameAs(config) || pollingDead)
                    {
                        var tokenChanged = !string.Equals(
                            running.Config.Token?.Trim(),
                            config.Token?.Trim(),
                            StringComparison.Ordinal);

                        if (pollingDead)
                        {
                            _logger.LogWarning(
                                "Telegram Bot 轮询已停止（曾发生 409 冲突），尝试恢复。Name={BotName}", config.Name);
                        }

                        await RemoveBotAsync(config.Name, running, deleteWebhook: tokenChanged, cancellationToken);
                        await StartBotAsync(config, cancellationToken);
                    }

                    continue;
                }

                await StartBotAsync(config, cancellationToken);
            }

            foreach (var name in _runningBots.Keys.ToList())
            {
                if (configMap.ContainsKey(name))
                {
                    continue;
                }

                if (_runningBots.TryGetValue(name, out var removed))
                {
                    await RemoveBotAsync(name, removed, deleteWebhook: true, cancellationToken);
                }
            }
        }
        finally
        {
            _ = _refreshLock.Release();
        }
    }

    private async Task<bool> UpdateSettingsAsync(CancellationToken cancellationToken, bool detectChanges)
    {
        var previousInterval = _refreshInterval;
        var settings = await LoadSettingsAsync(cancellationToken);
        var newBaseUrl = (settings.WebhookBaseUrl ?? string.Empty).Trim();
        var newRoutePrefix = NormalizeRoutePrefix(settings.WebhookRoutePrefix);
        var newSecretToken = (settings.WebhookSecretToken ?? string.Empty).Trim();
        var newNetwork = settings.Network ?? new TelegramBotNetworkOptions();

        var webhookChanged = !string.Equals(_webhookBaseUrl, newBaseUrl, StringComparison.OrdinalIgnoreCase)
                             || !string.Equals(_webhookRoutePrefix, newRoutePrefix, StringComparison.OrdinalIgnoreCase)
                             || !string.Equals(_webhookSecretToken, newSecretToken, StringComparison.Ordinal);
        var networkChanged = !_networkOptions.IsSameAs(newNetwork);

        _enabled = settings.Enabled;
        _webhookBaseUrl = newBaseUrl;
        _webhookRoutePrefix = newRoutePrefix;
        _webhookSecretToken = newSecretToken;
        _networkOptions = newNetwork;

        var refreshSeconds = settings.ManagerRefreshSeconds;
        var newInterval = refreshSeconds > 0 ? TimeSpan.FromSeconds(refreshSeconds) : TimeSpan.Zero;
        _refreshInterval = newInterval;

        // 仅在“无循环 → 有循环”时重建刷新循环：此时不存在旧循环，不会取消执行中刷新正在使用的令牌。
        // 正值间的变更由 RunRefreshLoopAsync 每轮重读 _refreshInterval 自动生效；
        // 正值 → 0 时循环在下一轮自行退出。无条件 ResetRefreshLoop 会中断本轮 diff（本方法通常运行在刷新循环内）
        if (_started && previousInterval <= TimeSpan.Zero && newInterval > TimeSpan.Zero)
        {
            ResetRefreshLoop();
        }

        // 传输或网络配置变更均需要重建全部机器人
        return detectChanges && (webhookChanged || networkChanged);
    }

    private async Task<TelegramBotSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ITelegramBotSettingsStore>();
        return await store.GetSettingsAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TelegramBotConfig>> LoadConfigsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ITelegramBotConfigStore>();
        var bots = await store.GetBotConfigsAsync(cancellationToken);
        return NormalizeConfigs(bots);
    }

    private IReadOnlyList<TelegramBotConfig> NormalizeConfigs(IReadOnlyList<TelegramBotConfig>? bots)
    {
        if (bots is not { Count: > 0 })
        {
            return [];
        }

        var normalizedBots = new List<TelegramBotConfig>(bots.Count);
        var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var bot in bots)
        {
            if (bot is null)
            {
                continue;
            }

            var name = (bot.Name ?? string.Empty).Trim();
            var token = (bot.Token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!nameSet.Add(name))
            {
                _logger.LogWarning("Telegram Bot 名称重复，已忽略：{BotName}", name);
                continue;
            }

            normalizedBots.Add(new TelegramBotConfig
            {
                Id = bot.Id,
                Name = name,
                Token = token,
                AdminUsers = [.. (bot.AdminUsers ?? []).Distinct().Where(x => x > 0)],
                AllowedGroupChatIds = [.. (bot.AllowedGroupChatIds ?? []).Distinct().Where(x => x != 0)],
                AllowedCommands = [.. (bot.AllowedCommands ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)],
                Remark = bot.Remark,
                EnableFallbackReply = bot.EnableFallbackReply
            });
        }

        return normalizedBots;
    }

    private async Task StartBotAsync(TelegramBotConfig config, CancellationToken cancellationToken)
    {
        BotInstance bot;
        try
        {
            bot = new BotInstance(config, _networkOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram Bot 实例创建失败。Name={BotName}", config.Name);
            return;
        }

        _registry.AddOrUpdate(bot);
        try
        {
            var started = UseWebhook
                ? await RegisterWebhookAsync(bot, cancellationToken)
                : await StartPollingAsync(bot, cancellationToken);
            if (!started)
            {
                _ = _runningBots.TryRemove(bot.Name, out _);
                _ = _registry.Remove(bot.Name);
                _logger.LogWarning(
                    "Telegram Bot 启动跳过（传输启动失败）。Name={BotName}, Mode={Mode}",
                    bot.Name, UseWebhook ? "webhook" : "polling");
                return;
            }

            var me = await bot.Client.GetMe(cancellationToken);
            bot.SetIdentity(me.Id, me.Username);
            _runningBots[bot.Name] = bot;

            await TrySetMyCommandsAsync(bot, cancellationToken);

            _logger.LogInformation(
                "Telegram Bot 已启动。Name={BotName}, Mode={Mode}, Username=@{Username}, Id={BotId}",
                bot.Name, UseWebhook ? "webhook" : "polling", me.Username, me.Id);
        }
        catch (Exception ex)
        {
            StopPolling(bot.Name);
            if (!UseWebhook)
            {
                await TryDeleteWebhookAsync(bot, cancellationToken);
            }

            _ = _runningBots.TryRemove(bot.Name, out _);
            _ = _registry.Remove(bot.Name);
            _logger.LogError(ex, "Telegram Bot 启动失败。Name={BotName}", bot.Name);
        }
    }

    private async Task RemoveBotAsync(string name, BotInstance bot, bool deleteWebhook, CancellationToken cancellationToken)
    {
        StopPolling(name);
        if (deleteWebhook)
        {
            await TryDeleteWebhookAsync(bot, cancellationToken);
        }

        _ = _runningBots.TryRemove(name, out _);
        _ = _registry.Remove(name);
        _logger.LogInformation("Telegram Bot 已移除。Name={BotName}", name);
    }

    private async Task StopAllBotsAsync(bool deleteWebhook, CancellationToken cancellationToken)
    {
        foreach (var item in _runningBots.ToList())
        {
            await RemoveBotAsync(item.Key, item.Value, deleteWebhook, cancellationToken);
        }
    }

    private async Task<bool> RegisterWebhookAsync(BotInstance bot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_webhookBaseUrl))
        {
            _logger.LogWarning("WebhookBaseUrl 未配置，无法注册 Webhook。Bot={BotName}", bot.Name);
            return false;
        }

        // fail-closed：Webhook 模式强制要求密钥令牌，未配置时拒绝注册，绝不允许无鉴权运行
        if (string.IsNullOrWhiteSpace(_webhookSecretToken))
        {
            _logger.LogError(
                "WebhookSecretToken 未配置，Webhook 模式必须设置密钥令牌，拒绝注册。Bot={BotName}", bot.Name);
            return false;
        }

        var url = BuildWebhookUrl(bot.Name);
        try
        {
            await bot.Client.SetWebhook(
                url: url,
                secretToken: _webhookSecretToken,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram Webhook 注册失败。Bot={BotName}, Url={Url}", bot.Name, url);
            return false;
        }
    }

    private async Task<bool> StartPollingAsync(BotInstance bot, CancellationToken cancellationToken)
    {
        var runningCts = _runningCts;
        if (runningCts is null || runningCts.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            StopPolling(bot.Name);
            await bot.Client.DeleteWebhook(cancellationToken: cancellationToken);

            // 接收器生命周期只链管理器整体运行令牌：调用方令牌（刷新循环 / 管理端请求）只作用于启动过程本身，
            // 避免刷新循环重置（如修改 ManagerRefreshSeconds）或请求中止连带杀死已启动机器人的轮询
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(runningCts.Token);
            try
            {
                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates =
                    [
                        UpdateType.Message,
                        UpdateType.EditedMessage,
                        UpdateType.ChannelPost,
                        UpdateType.EditedChannelPost,
                        UpdateType.CallbackQuery,
                        UpdateType.InlineQuery
                    ]
                };

                bot.Client.StartReceiving(
                    updateHandler: (_, update, ct) => HandlePollingUpdateAsync(bot, update, ct),
                    errorHandler: (_, exception, source, ct) => HandlePollingErrorAsync(bot, exception, source, ct),
                    receiverOptions: receiverOptions,
                    cancellationToken: linkedCts.Token);

                _pollingBotTokens[bot.Name] = linkedCts;
                return true;
            }
            catch
            {
                linkedCts.Cancel();
                linkedCts.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram Bot 轮询启动失败。Name={BotName}", bot.Name);
            return false;
        }
    }

    private async Task TrySetMyCommandsAsync(BotInstance bot, CancellationToken cancellationToken)
    {
        try
        {
            var allowedCommands = bot.Config.AllowedCommands;
            var commands = _catalog.GetPublicCommands(allowedCommands);
            var groupCommands = _catalog.GetPublicCommands(allowedCommands, preferAliasDescription: true);

            // 私聊默认菜单
            await SetOrDeleteMyCommandsAsync(bot.Client, commands, BotCommandScope.Default(), cancellationToken);
            // 群组菜单
            await SetOrDeleteMyCommandsAsync(bot.Client, groupCommands, BotCommandScope.AllGroupChats(), cancellationToken);
            _logger.LogInformation(
                "Telegram Bot 命令菜单同步成功。Bot={BotName}, Commands={CommandCount}, GroupCommands={GroupCommandCount}",
                bot.Name, commands.Count, groupCommands.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram Bot 命令菜单注册失败。Bot={BotName}", bot.Name);
        }
    }

    private static async Task SetOrDeleteMyCommandsAsync(
        ITelegramBotClient client,
        IReadOnlyList<BotCommand> commands,
        BotCommandScope scope,
        CancellationToken cancellationToken)
    {
        if (commands.Count == 0)
        {
            await client.DeleteMyCommands(scope: scope, cancellationToken: cancellationToken);
            return;
        }

        await client.SetMyCommands(commands, scope: scope, cancellationToken: cancellationToken);
    }

    private async Task TryDeleteWebhookAsync(BotInstance bot, CancellationToken cancellationToken)
    {
        try
        {
            await bot.Client.DeleteWebhook(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram Webhook 删除失败。Bot={BotName}", bot.Name);
        }
    }

    private void StopPolling(string botName)
    {
        if (!_pollingBotTokens.TryRemove(botName, out var cts))
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    private Task HandlePollingUpdateAsync(BotInstance bot, Update update, CancellationToken cancellationToken)
    {
        // 不 await，立即返回让轮询循环继续拉取下一条 update；
        // 通过 SemaphoreSlim 控制并发上限，防止瞬时过载
        try
        {
            _ = DispatchInBackgroundAsync(bot, update, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // 并发闸已释放，管理器正在关闭
        }

        return Task.CompletedTask;
    }

    private async Task DispatchInBackgroundAsync(BotInstance bot, Update update, CancellationToken cancellationToken)
    {
        try
        {
            await DispatchAsync(bot, update, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram Update 后台分发失败。Bot={BotName}, UpdateId={UpdateId}", bot.Name, update.Id);
        }
    }

    private Task HandlePollingErrorAsync(
        BotInstance bot,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        if (exception is ApiRequestException { ErrorCode: 409 } conflict)
        {
            // 另一实例正在 getUpdates（如同 Token 双实例部署），停掉本实例轮询避免互抢；
            // 刷新循环会在后续周期检测到轮询已停并尝试恢复
            StopPolling(bot.Name);
            _logger.LogWarning(conflict,
                "Telegram Bot 轮询检测到 getUpdates 冲突，已停止本实例轮询，将在下个刷新周期尝试恢复。Name={BotName}, Source={Source}",
                bot.Name, source);
            return Task.CompletedTask;
        }

        _logger.LogError(exception, "Telegram Bot 轮询监听异常。Name={BotName}, Source={Source}", bot.Name, source);
        return Task.CompletedTask;
    }

    private string BuildWebhookUrl(string botName)
    {
        var baseUrl = _webhookBaseUrl.TrimEnd('/');
        var prefix = _webhookRoutePrefix.TrimEnd('/');
        var safeName = Uri.EscapeDataString(botName);
        return $"{baseUrl}{prefix}/{safeName}";
    }
}
