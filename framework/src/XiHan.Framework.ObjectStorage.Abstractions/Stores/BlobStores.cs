// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 命名存储实例的注册表（单例）
/// </summary>
/// <remarks>
/// <para>按 <see cref="BlobStoreDefinition"/> 经对应的 <see cref="IBlobStoreProvider"/> 建实例，按名缓存。实例有两个来源：配置节 <c>XiHan:ObjectStorage:Stores:&lt;name&gt;</c>（<c>Provider</c> 加该 Provider 的设置，首次取用时建出），与应用运行时登记（<see cref="GetOrAddAsync"/>，如存放在库里的存储配置）。同名同定义返回同一实例；定义变了（Provider 或任一设置不同）建新实例替换旧的并释放旧的——替换时仍在旧实例上进行的调用可能失败。缺省的 <see cref="IBlobStore"/> 注入方式不受影响</para>
/// <para>容量：已建的实例数不超过 <see cref="MaxCount"/>（<c>XiHan:ObjectStorage:StoreMaxCount</c>，缺省 64，1 到 <see cref="MaxCountLimit"/>），两个来源的实例都算。满了再登记新名字返回 <see cref="ObjectStorageErrors.StoreCapacityExceeded"/>，不淘汰已有的；替换同名的不占名额，<see cref="RemoveAsync"/> 腾出名额（ADR-0266）</para>
/// <para>健康：每个实例一条 <c>objectstorage.store.&lt;name&gt;</c>，按 <c>XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds</c>（缺省 15 秒）在后台逐个探测，新实例建出后立即探一次；尚未探过的条目为 Started</para>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class BlobStores : IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 配置定义的实例所在的配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:ObjectStorage:Stores";

    /// <summary>
    /// 健康项名的前缀
    /// </summary>
    public const string HealthPrefix = "objectstorage.store.";

    /// <summary>
    /// 登记数上限的上限
    /// </summary>
    public const int MaxCountLimit = 1024;

    /// <summary>
    /// 定义里 Provider 名的配置键
    /// </summary>
    private const string ProviderKey = "Provider";

    /// <summary>
    /// 登记数上限的配置键
    /// </summary>
    /// <remarks>
    /// 不放在 <see cref="ConfigurationSection"/> 之下：那一节的子键都是实例名，配置键不区分大小写，<c>maxcount</c> 本身是合法的实例名
    /// </remarks>
    private const string MaxCountKey = "XiHan:ObjectStorage:StoreMaxCount";

    /// <summary>
    /// 登记数上限的缺省值
    /// </summary>
    private const int DefaultMaxCount = 64;

    /// <summary>
    /// Provider，按名（不区分大小写）
    /// </summary>
    private readonly Dictionary<string, IBlobStoreProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 配置
    /// </summary>
    /// <remarks>
    /// 配置定义的实例从这里读
    /// </remarks>
    private readonly IConfiguration? _configuration;

    /// <summary>
    /// 时钟：健康探测的定时器
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 探测间隔，也是单次探测的超时
    /// </summary>
    private readonly TimeSpan _probeInterval;

    /// <summary>
    /// 串行化建、替换与移除
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 停止时取消进行中的探测
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 已建的实例，按名
    /// </summary>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 健康探测的定时器
    /// </summary>
    /// <remarks>
    /// 建出第一个实例时启动
    /// </remarks>
    private ITimer? _timer;

    /// <summary>
    /// 是否正在探测（定时器回调不重叠）
    /// </summary>
    private int _probing;

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// 容器解析时 <paramref name="providers"/> 是全部登记的 Provider
    /// <c>configuration</c> 取 <c>null</c>
    /// </remarks>
    /// <param name="providers">Provider；同名重复是装配错误</param>
    /// <param name="clock">时钟</param>
    /// <param name="loggerFactory">日志工厂</param>
    public BlobStores(IEnumerable<IBlobStoreProvider> providers, TimeProvider clock, ILoggerFactory loggerFactory)
        : this(providers, clock, loggerFactory, null)
    {
    }

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// 容器解析时 <paramref name="providers"/> 是全部登记的 Provider
    /// </remarks>
    /// <param name="providers">Provider；同名重复是装配错误</param>
    /// <param name="clock">时钟</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="configuration">配置；为 <see langword="null"/> 时没有配置定义的实例，登记数上限与探测间隔取缺省值</param>
    /// <exception cref="ArgumentException">Provider 同名重复，或 <c>XiHan:ObjectStorage:StoreMaxCount</c>、<c>XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds</c> 的值不合法</exception>
    public BlobStores(IEnumerable<IBlobStoreProvider> providers, TimeProvider clock, ILoggerFactory loggerFactory, IConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<BlobStores>();
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(providers));
            if (!_providers.TryAdd(provider.Name, provider))
            {
                throw new ArgumentException($"Blob store provider '{provider.Name}' is registered twice.", nameof(providers));
            }
        }

        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _configuration = configuration;
        _probeInterval = (configuration is null ? null : ConfigurationValues.ReadSeconds(configuration, "XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds"))
            ?? TimeSpan.FromSeconds(15);
        MaxCount = (configuration is null ? null : ConfigurationValues.ReadInt32(configuration, MaxCountKey, 1, MaxCountLimit))
            ?? DefaultMaxCount;
    }

    /// <summary>
    /// 已登记的 Provider 名
    /// </summary>
    public IReadOnlyCollection<string> Providers => _providers.Keys;

    /// <summary>
    /// 最多同时有多少个已建的实例
    /// </summary>
    /// <remarks>
    /// <c>XiHan:ObjectStorage:StoreMaxCount</c>，1 到 <see cref="MaxCountLimit"/>，缺省 64。满了再登记新名字返回 <see cref="ObjectStorageErrors.StoreCapacityExceeded"/>，不淘汰已有的；替换同名的不占名额
    /// </remarks>
    public int MaxCount { get; }

    /// <summary>
    /// 已建的与配置里定义的实例名，按序数排序
    /// </summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            lock (_entries)
            {
                names.UnionWith(_entries.Keys);
            }

            foreach (var section in _configuration?.GetSection(ConfigurationSection).GetChildren() ?? [])
            {
                if (BlobStoreDefinition.IsValidName(section.Key))
                {
                    names.Add(section.Key);
                }
            }

            return [.. names];
        }
    }

    /// <summary>
    /// 取实例
    /// </summary>
    /// <remarks>
    /// 已建的直接返回；配置里有定义的首次取用时建出，建出时与 <see cref="GetOrAddAsync"/> 一样受登记数上限约束（满了返回 <see cref="ObjectStorageErrors.StoreCapacityExceeded"/>）。都没有返回 <see cref="ObjectStorageErrors.StoreNotFound"/>，配置定义不合法返回 <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>
    /// </remarks>
    public async ValueTask<Result<IBlobStore>> GetAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_entries)
        {
            if (_entries.TryGetValue(name, out var entry))
            {
                return Result.Success(entry.Store);
            }
        }

        var configured = BlobStoreDefinition.IsValidName(name) ? Configured(name) : null;
        return configured is null
            ? Error.NotFound(ObjectStorageErrors.StoreNotFound, ObjectStorageMessages.StoreNotFound, MessageArguments.Of(("store", name)))
            : await GetOrAddAsync(configured, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按定义取实例
    /// </summary>
    /// <remarks>
    /// 同名同定义返回已有的；没有或定义变了就建（替换并释放旧实例）。登记已满（已建的实例数达到 <see cref="MaxCount"/>）再登记新名字返回 <see cref="ObjectStorageErrors.StoreCapacityExceeded"/>（参数 <c>store</c>、<c>capacity</c>），不建实例、不淘汰已有的，替换同名的不占名额；Provider 未登记或设置不合法返回 <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>，后端建不出来（I/O 或权限错误）返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，建实例时的探测发现实例不能用返回 Provider 给出的错误码（如 S3 探明服务不认条件写入时 <see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>，探测被拒绝时 <see cref="ObjectStorageErrors.ProbeDenied"/>，探测桶不存在时 <see cref="ObjectStorageErrors.BlobNotFound"/>）；这些情况已有实例都不受影响
    /// </remarks>
    public async ValueTask<Result<IBlobStore>> GetOrAddAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Entry? replaced = null;
        Entry added;
        try
        {
            lock (_entries)
            {
                if (_entries.TryGetValue(definition.Name, out var existing))
                {
                    if (existing.Definition.Describes(definition))
                    {
                        return Result.Success(existing.Store);
                    }
                }
                else if (_entries.Count >= MaxCount)
                {
                    // 容量只拦新名字：替换同名的不增加登记数；满了不淘汰已有的，淘汰会让正在用它的调用落到已释放的实例上。
                    // 建、替换与移除都在 _gate 里串行，这里判过之后到放进登记表之前登记数不会变，并发登记超不过上限
                    return Error.Unavailable(ObjectStorageErrors.StoreCapacityExceeded, ObjectStorageMessages.StoreCapacityExceeded, MessageArguments.Of(("store", definition.Name), ("capacity", MaxCount)));
                }
            }

            if (!_providers.TryGetValue(definition.Provider, out var provider))
            {
                return Error.Validation(ObjectStorageErrors.StoreDefinitionInvalid, ObjectStorageMessages.StoreProviderUnknown, MessageArguments.Of(("store", definition.Name), ("provider", definition.Provider)));
            }

            // Provider 以结果报告失败（ADR-0150）：定义不合法、后端此刻建不出来、探测发现不能用都原样交给调用方，不登记，下次取用重新建
            var created = await provider.CreateAsync(definition, cancellationToken).ConfigureAwait(false);
            if (!created.TryGetValue(out var store))
            {
                LogCreateFailed(_logger, definition.Name, created.Error!.Code);
                return created.Error;
            }

            added = new Entry(definition, provider, store, new ContributorStatus(HealthPrefix + definition.Name, LifecycleState.Started)
            {
                Data = new Dictionary<string, object?>(StringComparer.Ordinal) { ["provider"] = provider.Name },
            });
            lock (_entries)
            {
                _entries.TryGetValue(definition.Name, out replaced);
                _entries[definition.Name] = added;
            }

            _timer ??= _clock.CreateTimer(static state => ((BlobStores)state!).ProbeInBackground(), this, _probeInterval, _probeInterval);
        }
        finally
        {
            _gate.Release();
        }

        if (replaced is not null)
        {
            await DisposeStoreAsync(replaced.Store).ConfigureAwait(false);
        }

        // 新实例立即探一次，不等下一个周期；探测结果只影响健康项
        _ = ProbeEntryAsync(added, _stopping.Token);
        return Result.Success(added.Store);
    }

    /// <summary>
    /// 移除并释放实例（批量）
    /// </summary>
    /// <remarks>
    /// 逐项返回该名字的实例是否存在过，与入参一一对应；同一批里重复的名字第二次为 <see langword="false"/>；空集合返回空列表。进入前检查取消，整批在一次加锁里移除，不会只移一半；移除之后逐个释放，释放不响应取消，某个实例释放失败只记警告、不影响其余实例与返回值。配置里有定义的实例之后再取用会重新建出。应用的存储配置改了、停用或删了时调用，腾出的名额可以再登记
    /// </remarks>
    /// <param name="names">实例名；有 <see langword="null"/> 是编程错误，抛出且不移除任何实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async ValueTask<IReadOnlyList<bool>> RemoveAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        for (var i = 0; i < names.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(names[i], nameof(names));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (names.Count == 0)
        {
            return [];
        }

        var results = new bool[names.Count];
        var removed = new List<Entry>();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 一次加锁移除整批：按入参顺序逐项施加，重复的名字第二次已不在
            lock (_entries)
            {
                for (var i = 0; i < names.Count; i++)
                {
                    if (_entries.Remove(names[i], out var entry))
                    {
                        results[i] = true;
                        removed.Add(entry);
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        foreach (var entry in removed)
        {
            try
            {
                await DisposeStoreAsync(entry.Store).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // 实例已经不在登记表里，结果已定：释放失败只记日志，不让它挡住后面几个实例的释放
                LogDisposeFailed(_logger, entry.Definition.Name, exception);
            }
        }

        return results;
    }

    /// <summary>
    /// 立即逐个探测全部已建实例并更新健康项
    /// </summary>
    public async ValueTask ProbeAsync(CancellationToken cancellationToken)
    {
        Entry[] entries;
        lock (_entries)
        {
            entries = [.. _entries.Values];
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProbeEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        lock (_entries)
        {
            return [.. _entries.Values.Select(static e => e.Status).OrderBy(static s => s.Name, StringComparer.Ordinal)];
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_stopping.IsCancellationRequested)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_timer is not null)
        {
            await _timer.DisposeAsync().ConfigureAwait(false);
        }

        Entry[] entries;
        lock (_entries)
        {
            entries = [.. _entries.Values];
            _entries.Clear();
        }

        foreach (var entry in entries)
        {
            await DisposeStoreAsync(entry.Store).ConfigureAwait(false);
        }

        _stopping.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// 配置里的定义
    /// </summary>
    /// <remarks>
    /// <c>Provider</c> 必填，其余键（相对路径）作为设置；没有该节时为 <see langword="null"/>
    /// </remarks>
    private BlobStoreDefinition? Configured(string name)
    {
        var section = _configuration?.GetSection($"{ConfigurationSection}:{name}");
        if (section is null || !section.GetChildren().Any())
        {
            return null;
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in section.AsEnumerable(makePathsRelative: true))
        {
            if (value is not null && !string.Equals(key, ProviderKey, StringComparison.OrdinalIgnoreCase))
            {
                settings[key] = value;
            }
        }

        return new BlobStoreDefinition(name, section[ProviderKey] ?? string.Empty, settings);
    }

    /// <summary>
    /// 定时器回调：上一轮还没探完就跳过这一轮
    /// </summary>
    private void ProbeInBackground()
    {
        if (Interlocked.Exchange(ref _probing, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ProbeAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 停止中
            }
            finally
            {
                Volatile.Write(ref _probing, 0);
            }
        });
    }

    /// <summary>
    /// 探测一个实例（超时为探测间隔）
    /// </summary>
    /// <remarks>
    /// 成功 Ready，失败或抛出 Degraded 并带错误；探测期间实例被替换或移除时结果丢弃
    /// </remarks>
    private async Task ProbeEntryAsync(Entry entry, CancellationToken cancellationToken)
    {
        ContributorStatus status;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_probeInterval);
        var started = _clock.GetTimestamp();
        try
        {
            var probed = await entry.Provider.ProbeAsync(entry.Store, timeout.Token).ConfigureAwait(false);
            status = probed.IsSuccess ? entry.Status with { State = LifecycleState.Ready, Error = null } : entry.Status with { State = LifecycleState.Degraded, Error = probed.Error };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // 探测按周期重复：已经 Degraded 时记 Debug，否则每个周期一条带堆栈的 Warning 会淹没日志
            if (entry.Status.State == LifecycleState.Degraded)
            {
                LogProbeStillThrowing(_logger, entry.Definition.Name, exception);
            }
            else
            {
                LogProbeThrew(_logger, entry.Definition.Name, exception);
            }

            status = entry.Status with { State = LifecycleState.Degraded, Error = BlobStoreFailures.Unavailable(exception) };
        }
        catch (OperationCanceledException)
        {
            return;
        }

        status = status with { CheckedAt = _clock.GetUtcNow(), Duration = _clock.GetElapsedTime(started) };

        lock (_entries)
        {
            if (_entries.TryGetValue(entry.Definition.Name, out var current) && ReferenceEquals(current, entry))
            {
                entry.Status = status;
            }
        }
    }

    /// <summary>
    /// 释放实例持有的资源
    /// </summary>
    private static async ValueTask DisposeStoreAsync(IBlobStore store)
    {
        switch (store)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    /// <summary>
    /// 一个已建的实例
    /// </summary>
    private sealed class Entry(BlobStoreDefinition definition, IBlobStoreProvider provider, IBlobStore store, ContributorStatus status)
    {
        /// <summary>
        /// 定义
        /// </summary>
        public BlobStoreDefinition Definition { get; } = definition;

        /// <summary>
        /// 建它的 Provider
        /// </summary>
        public IBlobStoreProvider Provider { get; } = provider;

        /// <summary>
        /// 实例
        /// </summary>
        public IBlobStore Store { get; } = store;

        /// <summary>
        /// 最近一次的健康状态
        /// </summary>
        public ContributorStatus Status { get; set; } = status;
    }

    /// <summary>
    /// 建实例失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Creating blob store '{Store}' failed with '{Code}'.")]
    private static partial void LogCreateFailed(ILogger logger, string store, string code);

    /// <summary>
    /// 移除的实例释放失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Disposing the removed blob store '{Store}' failed.")]
    private static partial void LogDisposeFailed(ILogger logger, string store, Exception exception);

    /// <summary>
    /// 探测抛出（从可用变成不可用）
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The health probe of blob store '{Store}' threw.")]
    private static partial void LogProbeThrew(ILogger logger, string store, Exception exception);

    /// <summary>
    /// 探测持续抛出
    /// </summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "The health probe of blob store '{Store}' still throws.")]
    private static partial void LogProbeStillThrowing(ILogger logger, string store, Exception exception);
}
