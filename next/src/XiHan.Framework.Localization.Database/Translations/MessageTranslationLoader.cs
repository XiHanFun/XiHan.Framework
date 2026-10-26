// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 把数据库里的翻译装进 <see cref="DatabaseMessageLocalizer"/>：宿主进入服务状态后首次加载，之后按周期刷新；写入翻译后可显式 <see cref="ReloadAsync"/>。读取经完整管道（仓储要求 Transaction 阶段）。
/// 同时是健康项 <see cref="DatabaseLocalizationFeature.HealthName"/> 的贡献者：以最近一次加载的结果报告，失败（保留上一份快照）为 Degraded。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class MessageTranslationLoader : IDisposable, IHealthContributor
{
    /// <summary>
    /// 根服务提供者；每次加载开新作用域。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 应用；加载经它的管道执行。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 被装载的本地化器。
    /// </summary>
    private readonly DatabaseMessageLocalizer _localizer;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly DatabaseLocalizationOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 最近一次加载的失败；成功后清空。后台循环写、健康采集读，整体替换不加锁。
    /// </summary>
    private volatile Error? _lastError;

    /// <summary>
    /// 是否已成功加载过一次。
    /// </summary>
    private volatile bool _loaded;

    /// <summary>
    /// 停止信号。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 后台循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 创建。
    /// </summary>
    public MessageTranslationLoader(IServiceProvider services, App app, DatabaseMessageLocalizer localizer, DatabaseLocalizationOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<MessageTranslationLoader>();
    }

    /// <summary>
    /// 最近一次成功加载的时刻；还没有加载过为 <see langword="null"/>。
    /// </summary>
    public DateTimeOffset? LastLoadedAt { get; private set; }

    /// <summary>
    /// 最近一次加载的失败；成功后为 <see langword="null"/>。失败时上一份快照仍在生效。
    /// </summary>
    public Error? LastError => _lastError;

    /// <summary>
    /// 立即重新加载并替换快照。只能在宿主服务期间调用；失败时保留上一份快照并返回失败。
    /// </summary>
    public async ValueTask<Result> ReloadAsync(CancellationToken cancellationToken)
    {
        var loaded = new List<MessageTranslation>();
        var truncated = false;
        await using var scope = _services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken);
        var outcome = await _app.InvokeAsync(context, async ctx =>
        {
            var repository = ctx.Services.GetRequiredService<IRepository<MessageTranslation, Guid>>();
            var skip = 0;
            while (true)
            {
                // 按 ID 分页读取，直到不足一页或达到上限；上限之外的翻译不装载，避免快照无界增长
                var take = Math.Min(EntityQuery.DefaultMaxTake, _options.MaxTranslations - loaded.Count);
                var page = await repository.ListAsync(EntityQuery.All<MessageTranslation>().OrderBy(static t => t.Id).Page(skip, take), ctx.CancellationToken).ConfigureAwait(false);
                loaded.AddRange(page);
                if (page.Count < take)
                {
                    return;
                }

                skip += page.Count;
                if (loaded.Count >= _options.MaxTranslations)
                {
                    truncated = true;
                    return;
                }
            }
        }).ConfigureAwait(false);

        if (outcome.IsFailure)
        {
            // 失败只记下来供健康项上报，快照不动
            _lastError = outcome.Error;
            LogLoadFailed(_logger, outcome.Error!.Code);
            return outcome;
        }

        _localizer.Apply(TranslationOverlay.From(loaded));
        LastLoadedAt = _clock.GetUtcNow();
        _lastError = null;
        _loaded = true;
        if (truncated)
        {
            LogLimitReached(_logger, _options.MaxTranslations);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 最近一次加载失败 → Degraded（上一份快照仍在生效）；还没加载过（宿主尚未进入服务状态）→ Started；否则 Ready
        if (_lastError is { } error)
        {
            return [new ContributorStatus(DatabaseLocalizationFeature.HealthName, LifecycleState.Degraded, error)];
        }

        return [new ContributorStatus(DatabaseLocalizationFeature.HealthName, _loaded ? LifecycleState.Ready : LifecycleState.Started)];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
    }

    /// <summary>
    /// 启动后台加载。
    /// </summary>
    internal void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _stopping = new CancellationTokenSource();
        var stopping = _stopping.Token;
        _loop = Task.Run(() => RunAsync(stopping), CancellationToken.None);
    }

    /// <summary>
    /// 停止后台加载。
    /// </summary>
    internal async Task StopAsync()
    {
        if (_loop is null || _stopping is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 按取消退出
        }

        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <summary>
    /// 等宿主进入服务状态后首次加载，再按周期刷新。
    /// </summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 宿主未进入服务状态前不能发起调用：启动阶段只等待
            while (!_app.State.IsServing())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), _clock, cancellationToken).ConfigureAwait(false);
            }

            await ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (_options.RefreshInterval is not { } interval)
            {
                return;
            }

            using var timer = new PeriodicTimer(interval, _clock);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 停止
        }
    }

    /// <summary>
    /// 加载失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading message translations failed: {ErrorCode}; the previous snapshot stays in effect.")]
    private static partial void LogLoadFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 达到上限。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Only the first {Limit} message translations were loaded; raise XiHan:Localization:Database:MaxTranslations to load the rest.")]
    private static partial void LogLimitReached(ILogger logger, int limit);
}
