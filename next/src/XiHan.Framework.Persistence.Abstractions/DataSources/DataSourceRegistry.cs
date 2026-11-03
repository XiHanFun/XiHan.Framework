// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 运行时数据源的注册表（单例）
/// </summary>
/// <remarks>
/// <para>按 <see cref="DataSourceDefinition"/> 经认得该数据库类型的 <see cref="IDataSourceProvider"/> 建数据源，按名缓存，登记数不超过 <see cref="DataSourceOptions.MaxCount"/>。同名同定义返回同一实例；定义变了建新实例替换旧的并释放旧的（替换时仍在旧实例上进行的调用可能失败）。运行时数据源与应用自己的连接互不相干：不进作用域客户端、不参与实体落点、不进工作单元</para>
/// <para>失效：记录改了、停用或删了调 <see cref="RemoveAsync"/>；每次使用都以记录里当前的定义调 <see cref="GetOrAddAsync"/>，定义变了即替换。注册表是进程内的，别的实例在下一次以新定义取用时替换，只按名字 <see cref="GetAsync"/> 看不到别的实例上的修改</para>
/// <para>健康：每个数据源一条 <c>persistence.datasource.&lt;name&gt;</c>，按 <see cref="DataSourceOptions.HealthProbeInterval"/> 在后台逐个打开连接再关闭，新登记的立即探一次；尚未探过 Started，成功 Ready，失败或超时 Degraded。连接串不进日志、错误与健康详情</para>
/// <para>没有登记任何 <see cref="IDataSourceProvider"/> 时（EF Core 与内存实现）取用与测试连接返回 <see cref="PersistenceErrors.NotSupported"/></para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DataSourceRegistry : IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 健康项名的前缀
    /// </summary>
    public const string HealthPrefix = "persistence.datasource.";

    /// <summary>
    /// 登记的 Provider，按登记顺序
    /// </summary>
    private readonly IDataSourceProvider[] _providers;

    /// <summary>
    /// 容量与探测间隔
    /// </summary>
    private readonly DataSourceOptions _options;

    /// <summary>
    /// 时钟：健康探测的定时器与探测时刻
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志：只记名字与数据库类型，不记连接串
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 串行化建、替换与移除
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 停止时取消进行中的探测
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 已登记的数据源，按名
    /// </summary>
    /// <remarks>
    /// 读写都在它自己的锁里；建实例、释放与探测在锁外进行
    /// </remarks>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 健康探测的定时器
    /// </summary>
    /// <remarks>
    /// 登记第一个数据源时启动
    /// </remarks>
    private ITimer? _timer;

    /// <summary>
    /// 是否正在探测（定时器回调不重叠）
    /// </summary>
    private int _probing;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="providers">Provider；为空表示当前的持久化 Provider 不支持运行时数据源</param>
    /// <param name="options">选项</param>
    /// <param name="clock">时钟</param>
    /// <param name="loggerFactory">日志工厂；为 <see langword="null"/> 时不记日志</param>
    public DataSourceRegistry(IEnumerable<IDataSourceProvider> providers, DataSourceOptions options, TimeProvider clock, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = [.. providers];
        foreach (var provider in _providers)
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(providers));
        }

        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DataSourceRegistry>();
    }

    /// <summary>
    /// 是否有支持运行时数据源的 Provider
    /// </summary>
    public bool IsSupported => _providers.Length > 0;

    /// <summary>
    /// 已登记的数据源名，按序数排序
    /// </summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries.Keys.Order(StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>
    /// 取本进程已登记的数据源
    /// </summary>
    /// <remarks>
    /// 没有登记返回 <see cref="PersistenceErrors.DataSourceNotFound"/>；不支持返回 <see cref="PersistenceErrors.NotSupported"/>。看不到别的实例上的修改：要用记录里当前的连接，调 <see cref="GetOrAddAsync"/>
    /// </remarks>
    public ValueTask<Result<IDataSource>> GetAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return ValueTask.FromResult<Result<IDataSource>>(NotSupported());
        }

        lock (_entries)
        {
            if (_entries.TryGetValue(name, out var entry))
            {
                return ValueTask.FromResult(Result.Success(entry.Source));
            }
        }

        return ValueTask.FromResult<Result<IDataSource>>(Error.NotFound(PersistenceErrors.DataSourceNotFound, PersistenceMessages.DataSourceNotFound, MessageArguments.Of(("name", name))));
    }

    /// <summary>
    /// 按定义取数据源
    /// </summary>
    /// <remarks>
    /// 同名同定义返回已有的；没有就建（不连库）；定义变了建新实例替换并释放旧的。数据库类型不认识或连接串格式错返回 <see cref="PersistenceErrors.DataSourceInvalid"/>，登记已满返回 <see cref="PersistenceErrors.DataSourceCapacityExceeded"/>，这几种情况同名的已登记实例都不受影响；不支持返回 <see cref="PersistenceErrors.NotSupported"/>
    /// </remarks>
    public async ValueTask<Result<IDataSource>> GetOrAddAsync(DataSourceDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!IsSupported)
        {
            return NotSupported();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Entry? replaced = null;
        Entry added;
        try
        {
            bool exists;
            lock (_entries)
            {
                exists = _entries.TryGetValue(definition.Name, out var existing);
                if (exists && existing!.Definition.Describes(definition))
                {
                    return Result.Success(existing.Source);
                }

                // 容量只拦新名字：替换同名的不增加登记数；满了不淘汰已有的，淘汰会让正在用它的调用悄悄换连接
                if (!exists && _entries.Count >= _options.MaxCount)
                {
                    return Error.Unavailable(PersistenceErrors.DataSourceCapacityExceeded, PersistenceMessages.DataSourceCapacityExceeded, MessageArguments.Of(("name", definition.Name), ("capacity", _options.MaxCount)));
                }
            }

            var created = await CreateAsync(definition, cancellationToken).ConfigureAwait(false);
            if (!created.TryGetValue(out var pair))
            {
                return created.Error!;
            }

            added = new Entry(definition, pair.Provider, pair.Source, new ContributorStatus(HealthPrefix + definition.Name, LifecycleState.Started)
            {
                Data = new Dictionary<string, object?>(StringComparer.Ordinal) { ["databaseType"] = definition.DatabaseType, ["provider"] = pair.Provider.Name },
            });
            lock (_entries)
            {
                _entries.TryGetValue(definition.Name, out replaced);
                _entries[definition.Name] = added;
            }

            _timer ??= _clock.CreateTimer(static state => ((DataSourceRegistry)state!).ProbeInBackground(), this, _options.HealthProbeInterval, _options.HealthProbeInterval);
        }
        finally
        {
            _gate.Release();
        }

        if (replaced is not null)
        {
            LogReplaced(_logger, definition.Name, definition.DatabaseType);
            await DisposeSourceAsync(replaced.Source).ConfigureAwait(false);
        }
        else
        {
            LogRegistered(_logger, definition.Name, definition.DatabaseType);
        }

        // 新实例立即探一次，不等下一个周期；探测结果只影响健康项
        _ = ProbeEntryAsync(added, _stopping.Token);
        return Result.Success(added.Source);
    }

    /// <summary>
    /// 移除并释放数据源（批量）
    /// </summary>
    /// <remarks>
    /// 逐项返回该名字是否登记过，与入参一一对应；同一批里重复的名字第二次为 <see langword="false"/>；空集合返回空列表；进入前检查取消。不支持时没有任何登记，逐项为 <see langword="false"/>。数据源记录改了、停用或删了时调用，旧的连接串不再被本进程使用
    /// </remarks>
    public async ValueTask<IReadOnlyList<bool>> RemoveAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
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
                    ArgumentNullException.ThrowIfNull(names[i], nameof(names));
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
            LogRemoved(_logger, entry.Definition.Name);
            await DisposeSourceAsync(entry.Source).ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>
    /// 测试连接：按定义建临时实例，打开一次连接再关闭，然后释放
    /// </summary>
    /// <remarks>
    /// 不登记、不占名额、不影响同名的已登记实例。成功返回成功；连不上返回 <see cref="PersistenceErrors.DataSourceUnavailable"/>（参数 <c>name</c>、<c>reason</c>，已脱敏）；定义不合法返回 <see cref="PersistenceErrors.DataSourceInvalid"/>；不支持返回 <see cref="PersistenceErrors.NotSupported"/>。等多久由 <paramref name="cancellationToken"/> 与连接串里驱动自己的连接超时决定
    /// </remarks>
    public async ValueTask<Result> TestAsync(DataSourceDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!IsSupported)
        {
            return NotSupported();
        }

        var created = await CreateAsync(definition, cancellationToken).ConfigureAwait(false);
        if (!created.TryGetValue(out var pair))
        {
            return created.Error!;
        }

        try
        {
            return await pair.Provider.ProbeAsync(pair.Source, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Provider 应当把连接失败作为结果返回；漏网的异常同样按连不上报告，原因先脱敏
            return Unavailable(definition, ConnectionStrings.Describe(exception, definition.ConnectionString));
        }
        finally
        {
            await DisposeSourceAsync(pair.Source).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 立即逐个探测全部已登记的数据源并更新健康项
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
            await DisposeSourceAsync(entry.Source).ConfigureAwait(false);
        }

        _stopping.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// 选 Provider 并建实例
    /// </summary>
    /// <remarks>
    /// 数据库类型没有 Provider 认得、Provider 以 <see cref="ArgumentException"/> 拒绝连接串，都翻译为 <see cref="PersistenceErrors.DataSourceInvalid"/>；原因经定义脱敏
    /// </remarks>
    private async ValueTask<Result<(IDataSourceProvider Provider, IDataSource Source)>> CreateAsync(DataSourceDefinition definition, CancellationToken cancellationToken)
    {
        var provider = Array.Find(_providers, p => p.Supports(definition.DatabaseType));
        if (provider is null)
        {
            return Error.Validation(PersistenceErrors.DataSourceInvalid, PersistenceMessages.DataSourceTypeUnknown, MessageArguments.Of(("name", definition.Name), ("databaseType", definition.DatabaseType)));
        }

        try
        {
            return (provider, await provider.CreateAsync(definition, cancellationToken).ConfigureAwait(false));
        }
        catch (ArgumentException exception)
        {
            return Error.Validation(PersistenceErrors.DataSourceInvalid, PersistenceMessages.DataSourceInvalid, MessageArguments.Of(("name", definition.Name), ("reason", definition.Redact(exception.Message))));
        }
    }

    /// <summary>
    /// 不支持运行时数据源
    /// </summary>
    private static Error NotSupported() => Error.Unavailable(PersistenceErrors.NotSupported, PersistenceMessages.DataSourcesNotSupported);

    /// <summary>
    /// 连不上：原因已脱敏
    /// </summary>
    private static Error Unavailable(DataSourceDefinition definition, string reason)
        => Error.Unavailable(PersistenceErrors.DataSourceUnavailable, PersistenceMessages.DataSourceUnavailable, MessageArguments.Of(("name", definition.Name), ("reason", reason)));

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
    /// 探测一个数据源（超时为探测间隔）
    /// </summary>
    /// <remarks>
    /// 成功 Ready，失败、抛出或超时 Degraded 并带脱敏的原因；探测期间数据源被替换或移除时结果丢弃
    /// </remarks>
    private async Task ProbeEntryAsync(Entry entry, CancellationToken cancellationToken)
    {
        ContributorStatus status;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.HealthProbeInterval);
        var started = _clock.GetTimestamp();
        try
        {
            var probed = await entry.Provider.ProbeAsync(entry.Source, timeout.Token).ConfigureAwait(false);
            status = probed.IsSuccess ? entry.Status with { State = LifecycleState.Ready, Error = null } : entry.Status with { State = LifecycleState.Degraded, Error = probed.Error };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 停止中：不改健康项
            return;
        }
        catch (Exception exception)
        {
            // 超时（探测自己的取消）与漏网的异常都按连不上报告；原因脱敏后才进健康详情
            var reason = exception is OperationCanceledException
                ? $"The probe did not finish within {_options.HealthProbeInterval.TotalSeconds} seconds."
                : ConnectionStrings.Describe(exception, entry.Definition.ConnectionString);
            status = entry.Status with { State = LifecycleState.Degraded, Error = Unavailable(entry.Definition, reason) };
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
    /// 释放数据源持有的资源
    /// </summary>
    private static async ValueTask DisposeSourceAsync(IDataSource source)
    {
        switch (source)
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
    /// 登记了新数据源
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Data source '{Name}' ({DatabaseType}) registered.")]
    private static partial void LogRegistered(ILogger logger, string name, string databaseType);

    /// <summary>
    /// 定义变了，替换了数据源
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Data source '{Name}' ({DatabaseType}) replaced because its definition changed.")]
    private static partial void LogReplaced(ILogger logger, string name, string databaseType);

    /// <summary>
    /// 移除了数据源
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Data source '{Name}' removed.")]
    private static partial void LogRemoved(ILogger logger, string name);

    /// <summary>
    /// 一个已登记的数据源
    /// </summary>
    private sealed class Entry(DataSourceDefinition definition, IDataSourceProvider provider, IDataSource source, ContributorStatus status)
    {
        /// <summary>
        /// 定义
        /// </summary>
        public DataSourceDefinition Definition { get; } = definition;

        /// <summary>
        /// 建它的 Provider
        /// </summary>
        public IDataSourceProvider Provider { get; } = provider;

        /// <summary>
        /// 数据源
        /// </summary>
        public IDataSource Source { get; } = source;

        /// <summary>
        /// 最近一次的健康状态
        /// </summary>
        public ContributorStatus Status { get; set; } = status;
    }
}
