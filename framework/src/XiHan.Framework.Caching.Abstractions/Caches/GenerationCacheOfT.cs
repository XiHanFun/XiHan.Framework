// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 架在 <see cref="ICache"/> 上、按范围失效并丢弃失效之前开始的读取的读穿缓存区
/// </summary>
/// <remarks>
/// 区与每个范围各有一个代数（随机令牌），也存在 <see cref="ICache"/> 里；条目写回时记下读取开始时的两个代数，读取时两个代数都还是当前值才算命中。
/// 失效即给范围（或整个区）换一个新代数，旧条目随之作废，不需要知道这个范围下有哪些条目。读取在查源之前取代数：失效之后才开始的读取看到新代数，失效之前开始的读取（可能读到写入提交之前的旧数据）写回的条目带着旧代数，没有人会把它当作命中（ADR-0194、ADR-0229）。
/// <see cref="ICache"/> 是两级缓存（<c>Caching.Hybrid</c>）时代数与条目都在本机一级缓存里命中，读取不出进程；一个实例的失效经共享存储的变更通知让所有实例的一级缓存丢掉旧代数。
/// 缓存后端故障（<see cref="FrameworkException"/> 携带 <see cref="CachingErrors.StoreUnavailable"/>）时读取绕过缓存直接查源、不写回，结果仍然正确；失效把异常抛给调用方，由它决定拒绝写入还是记日志
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class GenerationCache<T>
    where T : notnull
{
    /// <summary>
    /// 区名的最大长度
    /// </summary>
    /// <remarks>
    /// 留出后缀与两个编码段的余量，拼出的键不超过 <see cref="CacheKey.MaxLength"/>
    /// </remarks>
    public const int MaxRegionLength = 128;

    /// <summary>
    /// 键里直接写出的范围或条目文本的最大长度，更长的换成摘要
    /// </summary>
    private const int MaxLiteralLength = 128;

    /// <summary>
    /// 代数的存活时间是条目存活时间的倍数
    /// </summary>
    /// <remarks>
    /// 代数过期后下一次读取换新代数，该范围的条目随之作废；代数活得比条目久，正常情况下条目先过期
    /// </remarks>
    private const int GenerationLifetimeFactor = 10;

    /// <summary>
    /// 缓存后端故障时两次警告之间的最小间隔（毫秒）
    /// </summary>
    private const long WarningIntervalMilliseconds = 10_000;

    /// <summary>
    /// 底层缓存
    /// </summary>
    private readonly ICache _cache;

    /// <summary>
    /// 区名加分隔符，所有键的公共前缀
    /// </summary>
    private readonly string _prefix;

    /// <summary>
    /// 区代数的键
    /// </summary>
    private readonly CacheKey _regionKey;

    /// <summary>
    /// 条目的过期选项
    /// </summary>
    private readonly CacheEntryOptions _entryOptions;

    /// <summary>
    /// 代数的过期选项
    /// </summary>
    private readonly CacheEntryOptions _generationOptions;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 上一次记缓存后端故障警告的时刻（<see cref="Environment.TickCount64"/>）
    /// </summary>
    /// <remarks>
    /// 初值让第一次故障一定记录
    /// </remarks>
    private long _lastWarning = -WarningIntervalMilliseconds;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="cache">底层缓存，通常是容器里的 <see cref="ICache"/></param>
    /// <param name="region">区名，所有键的前缀，须是合法的缓存键文本且不超过 <see cref="MaxRegionLength"/></param>
    /// <param name="timeToLive">条目的存活时间：失效通知丢失或缓存后端故障时，旧值最多留这么久</param>
    /// <param name="logger">记录缓存后端故障</param>
    public GenerationCache(ICache cache, string region, TimeSpan timeToLive, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region.Length > MaxRegionLength || !CacheKey.IsValid(region))
        {
            throw new ArgumentException($"The region '{region}' must be a valid cache key of at most {MaxRegionLength} characters.", nameof(region));
        }

        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "The time to live must be positive.");
        }

        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _prefix = region + ":";
        _regionKey = new CacheKey(_prefix + "gen");
        TimeToLive = timeToLive;
        _entryOptions = CacheEntryOptions.Expires(timeToLive);
        // 代数比条目活得久；倍数会溢出时取最大值
        _generationOptions = CacheEntryOptions.Expires(timeToLive.Ticks > TimeSpan.MaxValue.Ticks / GenerationLifetimeFactor ? TimeSpan.MaxValue : timeToLive * GenerationLifetimeFactor);
    }

    /// <summary>
    /// 条目的存活时间
    /// </summary>
    public TimeSpan TimeToLive { get; }

    /// <summary>
    /// 批量读取，未命中的一次交给 <paramref name="load"/> 查源并写回
    /// </summary>
    /// <remarks>
    /// 结果与 <paramref name="keys"/> 顺序一一对应，同一位置重复出现只查一次；空集合不触缓存。<paramref name="load"/> 收到去重后的未命中位置，按同样的顺序返回值；返回失败时原样返回、不写缓存。缓存后端故障时全部交给 <paramref name="load"/>，不写回
    /// </remarks>
    public async ValueTask<Result<IReadOnlyList<T>>> GetOrLoadManyAsync(
        IReadOnlyList<GenerationCacheKey> keys,
        Func<IReadOnlyList<GenerationCacheKey>, CancellationToken, ValueTask<Result<IReadOnlyList<T>>>> load,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(load);
        if (keys.Count == 0)
        {
            return Result.Success<IReadOnlyList<T>>([]);
        }

        // 去重：同一位置只读一次、只查一次源，结果按原位置展开
        var distinct = new List<GenerationCacheKey>(keys.Count);
        var slotOf = new Dictionary<GenerationCacheKey, int>(keys.Count);
        var slots = new int[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            if (!slotOf.TryGetValue(key, out var slot))
            {
                slot = distinct.Count;
                distinct.Add(key);
                slotOf[key] = slot;
            }

            slots[index] = slot;
        }

        var values = new T[distinct.Count];
        var found = new bool[distinct.Count];
        string[] generations;
        Dictionary<string, int> scopeIndex;
        try
        {
            (generations, scopeIndex) = await ReadGenerationsAsync(distinct, cancellationToken).ConfigureAwait(false);
            var entries = await _cache.GetManyAsync<GenerationCacheEntry<T>>([.. distinct.Select(EntryKey)], cancellationToken).ConfigureAwait(false);
            for (var slot = 0; slot < distinct.Count; slot++)
            {
                // 两个代数都还是当前值才算命中：失效之前开始的读取写回的条目带着旧代数
                if (entries[slot].TryGetValue(out var entry)
                    && string.Equals(entry.Region, generations[0], StringComparison.Ordinal)
                    && string.Equals(entry.Scope, generations[scopeIndex[distinct[slot].Scope]], StringComparison.Ordinal))
                {
                    values[slot] = entry.Value;
                    found[slot] = true;
                }
            }
        }
        catch (FrameworkException exception) when (exception.Code == CachingErrors.StoreUnavailable)
        {
            // 缓存后端故障：直接查源、不写回。源是事实，结果仍然正确；故障不能变成「没有」或拒绝读取
            WarnUnavailable(exception);
            var direct = await load(distinct, cancellationToken).ConfigureAwait(false);
            return direct.TryGetValue(out var loadedDirectly) ? Result.Success<IReadOnlyList<T>>(Expand(Checked(loadedDirectly, distinct.Count), slots)) : direct.Error!;
        }

        var missing = new List<int>();
        for (var slot = 0; slot < distinct.Count; slot++)
        {
            if (!found[slot])
            {
                missing.Add(slot);
            }
        }

        if (missing.Count > 0)
        {
            var loaded = await load([.. missing.Select(slot => distinct[slot])], cancellationToken).ConfigureAwait(false);
            // 查源失败不写缓存：源恢复后下一次读取立即重新查，而不是在存活时间内一直报失败
            if (!loaded.TryGetValue(out var fresh))
            {
                return loaded.Error!;
            }

            Checked(fresh, missing.Count);
            var writes = new KeyValuePair<CacheKey, GenerationCacheEntry<T>>[missing.Count];
            for (var position = 0; position < missing.Count; position++)
            {
                var slot = missing[position];
                values[slot] = fresh[position];
                // 记下的是查源之前取到的代数：这期间有失效，条目就带着旧代数、不会被当作命中
                writes[position] = new(EntryKey(distinct[slot]), new GenerationCacheEntry<T>(generations[0], generations[scopeIndex[distinct[slot].Scope]], fresh[position]));
            }

            try
            {
                await _cache.SetManyAsync(writes, _entryOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (FrameworkException exception) when (exception.Code == CachingErrors.StoreUnavailable)
            {
                // 写回失败只是少缓存一次，值是刚从源读到的
                WarnUnavailable(exception);
            }
        }

        return Result.Success<IReadOnlyList<T>>(Expand(values, slots));
    }

    /// <summary>
    /// 让这些范围下的全部条目失效（所有共用底层缓存的实例）
    /// </summary>
    /// <remarks>
    /// 给每个范围换一个新代数，一次批量写入；空集合什么也不做。缓存后端故障时抛出，调用方决定拒绝写入（失效没有传到别的实例）还是记日志后靠 <see cref="TimeToLive"/> 收敛
    /// </remarks>
    public ValueTask InvalidateAsync(IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var writes = new List<KeyValuePair<CacheKey, string>>(scopes.Count);
        foreach (var scope in scopes)
        {
            ArgumentNullException.ThrowIfNull(scope, nameof(scopes));
            if (seen.Add(scope))
            {
                writes.Add(new(ScopeKey(scope), NewGeneration()));
            }
        }

        return _cache.SetManyAsync(writes, _generationOptions, cancellationToken);
    }

    /// <summary>
    /// 让区里的全部条目失效（所有共用底层缓存的实例）
    /// </summary>
    /// <remarks>
    /// 换区代数；缓存后端故障时抛出
    /// </remarks>
    public ValueTask InvalidateAllAsync(CancellationToken cancellationToken)
        => _cache.SetAsync(_regionKey, NewGeneration(), _generationOptions, cancellationToken);

    /// <summary>
    /// 取区代数与这些位置涉及的范围代数，缺的先换新并写下
    /// </summary>
    /// <remarks>
    /// 返回的数组第 0 项是区代数，其余按范围在 <c>scopeIndex</c> 里的下标
    /// </remarks>
    private async ValueTask<(string[] Generations, Dictionary<string, int> ScopeIndex)> ReadGenerationsAsync(List<GenerationCacheKey> keys, CancellationToken cancellationToken)
    {
        var scopeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var generationKeys = new List<CacheKey> { _regionKey };
        foreach (var key in keys)
        {
            if (!scopeIndex.ContainsKey(key.Scope))
            {
                scopeIndex[key.Scope] = generationKeys.Count;
                generationKeys.Add(ScopeKey(key.Scope));
            }
        }

        var read = await _cache.GetManyAsync<string>(generationKeys, cancellationToken).ConfigureAwait(false);
        var generations = new string[read.Count];
        List<KeyValuePair<CacheKey, string>>? minted = null;
        for (var index = 0; index < read.Count; index++)
        {
            if (read[index].TryGetValue(out var generation))
            {
                generations[index] = generation;
            }
            else
            {
                generations[index] = NewGeneration();
                (minted ??= []).Add(new(generationKeys[index], generations[index]));
            }
        }

        // 缺代数（从未写过、已过期或被淘汰）时先写下新代数再查源：之后的失效会覆盖它，
        // 查源读到的至少是写下之后的数据；新代数与任何旧条目都不相等，旧条目随之作废
        if (minted is not null)
        {
            await _cache.SetManyAsync(minted, _generationOptions, cancellationToken).ConfigureAwait(false);
        }

        return (generations, scopeIndex);
    }

    /// <summary>
    /// 条目的缓存键
    /// </summary>
    private CacheKey EntryKey(GenerationCacheKey key) => new(_prefix + "entry:" + Encode(key.Scope) + ":" + Encode(key.Entry));

    /// <summary>
    /// 范围代数的缓存键
    /// </summary>
    private CacheKey ScopeKey(string scope) => new(_prefix + "gen:" + Encode(scope));

    /// <summary>
    /// 把任意文本编成键的一段
    /// </summary>
    /// <remarks>
    /// 能直接写进键的写成 <c>&lt;长度&gt;.&lt;文本&gt;</c>，长度前缀让 <c>a:b</c> 与 <c>a</c>、<c>b</c> 拼出的键不同；含空白、控制字符或过长的写成 <c>#&lt;SHA-256 十六进制&gt;</c>，以 <c>#</c> 开头与前者不会相同
    /// </remarks>
    private static string Encode(string text)
    {
        if (text.Length <= MaxLiteralLength && (text.Length == 0 || CacheKey.IsValid(text)))
        {
            return text.Length.ToString(CultureInfo.InvariantCulture) + "." + text;
        }

        return "#" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>
    /// 新的代数
    /// </summary>
    private static string NewGeneration() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// 核对查源返回的条数
    /// </summary>
    /// <remarks>
    /// 条数不符是查源函数的编程错误
    /// </remarks>
    private static IReadOnlyList<T> Checked(IReadOnlyList<T> values, int expected)
        => values.Count == expected
            ? values
            : throw new InvalidOperationException($"The load function returned {values.Count} values for {expected} keys.");

    /// <summary>
    /// 把去重后的结果按原位置展开
    /// </summary>
    private static T[] Expand(IReadOnlyList<T> values, int[] slots)
    {
        var results = new T[slots.Length];
        for (var index = 0; index < slots.Length; index++)
        {
            results[index] = values[slots[index]];
        }

        return results;
    }

    /// <summary>
    /// 记缓存后端故障，按间隔节流
    /// </summary>
    /// <remarks>
    /// 故障期间每次读取都会走到这里，只每隔一段时间记一次，避免日志被刷满；健康项另行报告后端状态
    /// </remarks>
    private void WarnUnavailable(FrameworkException exception)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastWarning);
        if (now - last >= WarningIntervalMilliseconds && Interlocked.CompareExchange(ref _lastWarning, now, last) == last)
        {
            GenerationCacheLog.StoreUnavailable(_logger, _prefix.TrimEnd(':'), exception);
        }
    }
}
