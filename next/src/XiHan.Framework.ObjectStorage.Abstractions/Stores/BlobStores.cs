// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 命名存储实例的注册表（单例）：按 <see cref="BlobStoreDefinition"/> 经对应的 <see cref="IBlobStoreProvider"/> 建实例，按名缓存。
/// 实例有两个来源：配置节 <c>XiHan:ObjectStorage:Stores:&lt;name&gt;</c>（<c>Provider</c> 加该 Provider 的设置，首次取用时建出），与应用运行时登记（<see cref="GetOrAddAsync"/>，如存放在库里的存储配置）。
/// 同名同定义返回同一实例；定义变了（Provider 或任一设置不同）建新实例替换旧的并释放旧的——替换时仍在旧实例上进行的调用可能失败。缺省的 <see cref="IBlobStore"/> 注入方式不受影响。
/// 同时是健康项的贡献者：每个实例一条 <c>objectstorage.store.&lt;name&gt;</c>，按 <c>XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds</c>（缺省 15 秒）在后台逐个探测，新实例建出后立即探一次；尚未探过的条目为 Started。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class BlobStores : IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 配置定义的实例所在的配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:ObjectStorage:Stores";

    /// <summary>
    /// 健康项名的前缀。
    /// </summary>
    public const string HealthPrefix = "objectstorage.store.";

    /// <summary>
    /// 定义里 Provider 名的配置键。
    /// </summary>
    private const string ProviderKey = "Provider";

    /// <summary>
    /// Provider，按名（不区分大小写）。
    /// </summary>
    private readonly Dictionary<string, IBlobStoreProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 配置；配置定义的实例从这里读。
    /// </summary>
    private readonly IConfiguration? _configuration;

    /// <summary>
    /// 时钟：健康探测的定时器。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 探测间隔，也是单次探测的超时。
    /// </summary>
    private readonly TimeSpan _probeInterval;

    /// <summary>
    /// 串行化建、替换与移除。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 停止时取消进行中的探测。
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 已建的实例，按名。
    /// </summary>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 健康探测的定时器；建出第一个实例时启动。
    /// </summary>
    private ITimer? _timer;

    /// <summary>
    /// 是否正在探测（定时器回调不重叠）。
    /// </summary>
    private int _probing;

    /// <summary>
    /// 创建；容器解析时 <paramref name="providers"/> 是全部登记的 Provider。
    /// </summary>
    /// <param name="providers">Provider；同名重复是装配错误。</param>
    /// <param name="clock">时钟。</param>
    /// <param name="configuration">配置；为 <see langword="null"/> 时没有配置定义的实例。</param>
    public BlobStores(IEnumerable<IBlobStoreProvider> providers, TimeProvider clock, IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
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
        var interval = configuration?["XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds"];
        _probeInterval = interval is null
            ? TimeSpan.FromSeconds(15)
            : double.TryParse(interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && !double.IsInfinity(seconds)
                ? TimeSpan.FromSeconds(seconds)
                : throw new ArgumentException($"XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds must be a positive number, but was '{interval}'.");
    }

    /// <summary>
    /// 已登记的 Provider 名。
    /// </summary>
    public IReadOnlyCollection<string> Providers => _providers.Keys;

    /// <summary>
    /// 已建的与配置里定义的实例名，按序数排序。
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
    /// 取实例：已建的直接返回；配置里有定义的首次取用时建出。都没有返回 <see cref="ObjectStorageErrors.StoreNotFound"/>，配置定义不合法返回 <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>。
    /// </summary>
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
    /// 按定义取实例：同名同定义返回已有的；没有或定义变了就建（替换并释放旧实例）。Provider 未登记或设置不合法返回 <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>，后端建不出来（I/O 或权限错误）返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，两种情况已有实例都不受影响。
    /// </summary>
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
                if (_entries.TryGetValue(definition.Name, out var existing) && existing.Definition.Describes(definition))
                {
                    return Result.Success(existing.Store);
                }
            }

            if (!_providers.TryGetValue(definition.Provider, out var provider))
            {
                return Error.Validation(ObjectStorageErrors.StoreDefinitionInvalid, ObjectStorageMessages.StoreProviderUnknown, MessageArguments.Of(("store", definition.Name), ("provider", definition.Provider)));
            }

            IBlobStore store;
            try
            {
                store = await provider.CreateAsync(definition, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException exception)
            {
                return Error.Validation(ObjectStorageErrors.StoreDefinitionInvalid, ObjectStorageMessages.StoreDefinitionInvalid, MessageArguments.Of(("store", definition.Name), ("reason", exception.Message)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 后端此刻建不出来（目录无权限、磁盘错误）：不是定义的问题，下次取用会重试
                return Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", exception.Message)));
            }

            added = new Entry(definition, provider, store, new ContributorStatus(HealthPrefix + definition.Name, LifecycleState.Started));
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
    /// 移除并释放实例；返回是否存在过。配置里有定义的实例之后再取用会重新建出。
    /// </summary>
    public async ValueTask<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Entry? removed;
        try
        {
            lock (_entries)
            {
                if (!_entries.Remove(name, out removed))
                {
                    return false;
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        await DisposeStoreAsync(removed.Store).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 立即逐个探测全部已建实例并更新健康项。
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
    /// 配置里的定义：<c>Provider</c> 必填，其余键（相对路径）作为设置；没有该节时为 <see langword="null"/>。
    /// </summary>
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
    /// 定时器回调：上一轮还没探完就跳过这一轮。
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
    /// 探测一个实例（超时为探测间隔）：成功 Ready，失败或抛出 Degraded 并带错误；探测期间实例被替换或移除时结果丢弃。
    /// </summary>
    private async Task ProbeEntryAsync(Entry entry, CancellationToken cancellationToken)
    {
        ContributorStatus status;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_probeInterval);
        try
        {
            var probed = await entry.Provider.ProbeAsync(entry.Store, timeout.Token).ConfigureAwait(false);
            status = probed.IsSuccess ? entry.Status with { State = LifecycleState.Ready, Error = null } : entry.Status with { State = LifecycleState.Degraded, Error = probed.Error };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            status = entry.Status with { State = LifecycleState.Degraded, Error = Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", exception.Message))) };
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_entries)
        {
            if (_entries.TryGetValue(entry.Definition.Name, out var current) && ReferenceEquals(current, entry))
            {
                entry.Status = status;
            }
        }
    }

    /// <summary>
    /// 释放实例持有的资源。
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
    /// 一个已建的实例。
    /// </summary>
    private sealed class Entry(BlobStoreDefinition definition, IBlobStoreProvider provider, IBlobStore store, ContributorStatus status)
    {
        /// <summary>
        /// 定义。
        /// </summary>
        public BlobStoreDefinition Definition { get; } = definition;

        /// <summary>
        /// 建它的 Provider。
        /// </summary>
        public IBlobStoreProvider Provider { get; } = provider;

        /// <summary>
        /// 实例。
        /// </summary>
        public IBlobStore Store { get; } = store;

        /// <summary>
        /// 最近一次的健康状态。
        /// </summary>
        public ContributorStatus Status { get; set; } = status;
    }
}
