// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 把数据库里的翻译装进 <see cref="DatabaseMessageLocalizer"/>
/// </summary>
/// <remarks>
/// 宿主进入服务状态后首次加载。之后三种情况重新加载：<see cref="ICache"/> 里的版本戳（<see cref="VersionCacheKey"/>）变了——<see cref="MessageTranslationStore"/> 写入提交后经 <see cref="PublishChangeAsync"/> 换它，每个实例按 <see cref="DatabaseLocalizationOptions.ChangeCheckInterval"/> 核对（ADR-0229）；本实例写入后立即唤醒；按 <see cref="DatabaseLocalizationOptions.RefreshInterval"/> 周期刷新兜底。
/// 查找是同步的，所以翻译仍在进程内快照里，<see cref="ICache"/> 只承载跨实例的信号：两级缓存下核对版本戳是本机一级缓存命中，别的实例换了版本戳经变更通知让它失效。读版本戳遇到缓存后端故障时跳过这次核对，靠周期刷新收敛。
/// 重新加载彼此串行，后开始的读到的不早于先开始的。读取经完整管道（仓储要求 Transaction 阶段）。同时是健康项 <see cref="DatabaseLocalizationFeature.HealthName"/> 的贡献者：以最近一次加载的结果报告，失败（保留上一份快照）为 Degraded
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class MessageTranslationLoader : IDisposable, IHealthContributor
{
    /// <summary>
    /// 翻译版本戳在 <see cref="ICache"/> 里的键
    /// </summary>
    public const string VersionCacheKey = "localization:translations:version";

    /// <summary>
    /// 版本戳的存活时间
    /// </summary>
    /// <remarks>
    /// 过期后每个实例看到「没有版本戳」与上次记下的不同，各多一次重新加载，无害
    /// </remarks>
    private static readonly CacheEntryOptions _versionOptions = CacheEntryOptions.Expires(TimeSpan.FromDays(1));

    /// <summary>
    /// 版本戳的缓存键
    /// </summary>
    private static readonly CacheKey _versionKey = new(VersionCacheKey);

    /// <summary>
    /// 根服务提供者
    /// </summary>
    /// <remarks>
    /// 每次加载开新作用域
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 应用
    /// </summary>
    /// <remarks>
    /// 加载经它的管道执行
    /// </remarks>
    private readonly App _app;

    /// <summary>
    /// 被装载的本地化器
    /// </summary>
    private readonly DatabaseMessageLocalizer _localizer;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly DatabaseLocalizationOptions _options;

    /// <summary>
    /// 承载版本戳的缓存
    /// </summary>
    private readonly ICache _cache;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 串行化重新加载
    /// </summary>
    /// <remarks>
    /// 并发的两次加载可能后开始的先完成、先开始的带着旧数据后完成，把快照换回旧的
    /// </remarks>
    private readonly SemaphoreSlim _reloading = new(1, 1);

    /// <summary>
    /// 唤醒后台循环
    /// </summary>
    /// <remarks>
    /// 本实例写入翻译后释放一次，最多攒一次
    /// </remarks>
    private readonly SemaphoreSlim _wake = new(0, 1);

    /// <summary>
    /// 是否有人要求立即重新加载（0 / 1）
    /// </summary>
    private int _reloadRequested;

    /// <summary>
    /// 后台循环最近一次成功加载时，加载开始前读到的版本戳
    /// </summary>
    /// <remarks>
    /// 没有版本戳为 <see langword="null"/>；只在后台循环里读写
    /// </remarks>
    private string? _seenVersion;

    /// <summary>
    /// 最近一次加载的失败
    /// </summary>
    /// <remarks>
    /// 成功后清空。后台循环写、健康采集读，整体替换不加锁
    /// </remarks>
    private volatile Error? _lastError;

    /// <summary>
    /// 是否已成功加载过一次
    /// </summary>
    private volatile bool _loaded;

    /// <summary>
    /// 停止信号
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 后台循环
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 创建
    /// </summary>
    public MessageTranslationLoader(IServiceProvider services, App app, DatabaseMessageLocalizer localizer, DatabaseLocalizationOptions options, ICache cache, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<MessageTranslationLoader>();
    }

    /// <summary>
    /// 最近一次成功加载的时刻
    /// </summary>
    /// <remarks>
    /// 还没有加载过为 <see langword="null"/>
    /// </remarks>
    public DateTimeOffset? LastLoadedAt { get; private set; }

    /// <summary>
    /// 最近一次加载的失败
    /// </summary>
    /// <remarks>
    /// 成功后为 <see langword="null"/>。失败时上一份快照仍在生效
    /// </remarks>
    public Error? LastError => _lastError;

    /// <summary>
    /// 立即重新加载并替换快照
    /// </summary>
    /// <remarks>
    /// 只能在宿主服务期间调用；失败时保留上一份快照并返回失败。只作用于本实例，要让所有实例重新加载用 <see cref="PublishChangeAsync"/>
    /// </remarks>
    public async ValueTask<Result> ReloadAsync(CancellationToken cancellationToken)
    {
        await _reloading.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReloadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _reloading.Release();
        }
    }

    /// <summary>
    /// 让所有实例重新加载翻译
    /// </summary>
    /// <remarks>
    /// 在 <see cref="ICache"/> 里换一个新的版本戳，并唤醒本实例的后台循环立即重新加载；其他实例在 <see cref="DatabaseLocalizationOptions.ChangeCheckInterval"/> 内看到新版本戳。<see cref="MessageTranslationStore"/> 写入提交后自动调用；绕过它直接改表后手动调用。缓存后端故障时本实例照常重新加载，然后抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的异常
    /// </remarks>
    public async ValueTask PublishChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 先换版本戳再唤醒：本实例的这次加载记下的就是新版本戳，不会因为随后的核对再加载一次
            await _cache.SetAsync(_versionKey, Guid.NewGuid().ToString("N"), _versionOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RequestReload();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 最近一次加载失败 → Degraded（上一份快照仍在生效）；还没加载过（宿主尚未进入服务状态）→ Started；否则 Ready
        if (_lastError is { } error)
        {
            return [new ContributorStatus(DatabaseLocalizationFeature.HealthName, LifecycleState.Degraded) { Error = error }];
        }

        return [new ContributorStatus(DatabaseLocalizationFeature.HealthName, _loaded ? LifecycleState.Ready : LifecycleState.Started)];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
        _reloading.Dispose();
        _wake.Dispose();
    }

    /// <summary>
    /// 启动后台加载
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
    /// 停止后台加载
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
    /// 要求后台循环立即重新加载
    /// </summary>
    private void RequestReload()
    {
        Volatile.Write(ref _reloadRequested, 1);
        // 信号量最多攒一次：已经有一次未消费的唤醒时不再释放
        if (_wake.CurrentCount == 0)
        {
            try
            {
                _wake.Release();
            }
            catch (SemaphoreFullException)
            {
                // 与另一个写入者同时释放，已经有一次唤醒在等着
            }
        }
    }

    /// <summary>
    /// 重新加载并替换快照，调用方已持有 <see cref="_reloading"/>
    /// </summary>
    private async ValueTask<Result> ReloadCoreAsync(CancellationToken cancellationToken)
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

    /// <summary>
    /// 等宿主进入服务状态后首次加载，再按版本戳、唤醒与周期刷新重新加载
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

            await CheckAsync(force: true, cancellationToken).ConfigureAwait(false);
            var nextRefresh = _clock.GetUtcNow() + _options.RefreshInterval;
            while (true)
            {
                await WaitAsync(nextRefresh, cancellationToken).ConfigureAwait(false);
                var now = _clock.GetUtcNow();
                // 周期刷新到点或本实例要求过重新加载时不看版本戳，直接加载
                var due = nextRefresh is { } at && now >= at;
                var requested = Interlocked.Exchange(ref _reloadRequested, 0) == 1;
                await CheckAsync(due || requested, cancellationToken).ConfigureAwait(false);
                if (due)
                {
                    nextRefresh = now + _options.RefreshInterval;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 停止
        }
    }

    /// <summary>
    /// 等到下一次核对、周期刷新或被唤醒
    /// </summary>
    /// <remarks>
    /// 版本核对与周期刷新都关闭时只等唤醒
    /// </remarks>
    private async Task WaitAsync(DateTimeOffset? nextRefresh, CancellationToken cancellationToken)
    {
        var delay = _options.ChangeCheckInterval;
        if (nextRefresh is { } at)
        {
            var untilRefresh = at - _clock.GetUtcNow();
            untilRefresh = untilRefresh < TimeSpan.Zero ? TimeSpan.Zero : untilRefresh;
            delay = delay is { } check && check < untilRefresh ? check : untilRefresh;
        }

        if (delay is not { } wait)
        {
            await _wake.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // 计时与唤醒谁先到都结束等待，结束后取消另一个，不留悬挂的等待；唤醒若已拿到信号量，这次就算被唤醒
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var woken = _wake.WaitAsync(linked.Token);
        var elapsed = Task.Delay(wait, _clock, linked.Token);
        await Task.WhenAny(woken, elapsed).ConfigureAwait(false);
        await linked.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(woken, elapsed).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 输掉的一方按设计被取消；停止信号在下面单独核对
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 核对版本戳，变了（或 <paramref name="force"/>）就重新加载
    /// </summary>
    /// <remarks>
    /// 加载之前读版本戳：加载期间别的实例又换了版本戳，下一次核对还会看到不同、再加载一次
    /// </remarks>
    private async Task CheckAsync(bool force, CancellationToken cancellationToken)
    {
        string? version = null;
        var known = false;
        try
        {
            var read = await _cache.GetAsync<string>(_versionKey, cancellationToken).ConfigureAwait(false);
            version = read.TryGetValue(out var value) ? value : null;
            known = true;
        }
        catch (FrameworkException exception) when (exception.Code == CachingErrors.StoreUnavailable)
        {
            // 读不到版本戳就不知道变没变：不因此加载，周期刷新兜底；强制加载照常进行
            LogVersionUnavailable(_logger, exception);
        }

        if (!force && (!known || string.Equals(version, _seenVersion, StringComparison.Ordinal)))
        {
            return;
        }

        var outcome = await ReloadAsync(cancellationToken).ConfigureAwait(false);
        if (outcome.IsSuccess && known)
        {
            _seenVersion = version;
        }
    }

    /// <summary>
    /// 加载失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading message translations failed: {ErrorCode}; the previous snapshot stays in effect.")]
    private static partial void LogLoadFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 读不到版本戳
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the message translation version failed because the cache is unavailable; translations are reloaded at the next periodic refresh.")]
    private static partial void LogVersionUnavailable(ILogger logger, Exception exception);

    /// <summary>
    /// 达到上限
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Only the first {Limit} message translations were loaded; raise XiHan:Localization:Database:MaxTranslations to load the rest.")]
    private static partial void LogLimitReached(ILogger logger, int limit);
}
