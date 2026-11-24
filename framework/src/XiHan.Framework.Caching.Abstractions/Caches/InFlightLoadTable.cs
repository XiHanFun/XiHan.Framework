// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 在途表：让同一个键的并发回源只执行一次，其余调用等它的结果
/// </summary>
/// <remarks>
/// <see cref="ICache.GetOrCreateAsync"/> 的实现各持有一张（ADR-0271）。范围是持有它的那个缓存实例，跨实例、跨进程不合并。
/// <list type="bullet">
/// <item>先到的调用领头：回源函数在它自己的上下文里、用它自己的取消令牌执行，与没有合并时一样；其余调用等它的结局</item>
/// <item>等待的调用取消只结束它自己的等待。领头的调用取消时回源随它取消，等待的调用里先到的一个接手、执行它自己的回源函数</item>
/// <item>回源抛出（领头的调用没有取消）时，当时在等的调用都收到同一个异常；什么也不留下，下一次调用重新回源</item>
/// <item>表里只有正在进行的回源，结束（成功、失败、取消）或被取代即移除，条数不超过同时在回源的调用数</item>
/// <item>同一个键以不同的结果类型并发读取不合并；回源函数里再读调用链上正在回源的键也不合并（否则是等自己）。这两种情况照没有合并时各自回源</item>
/// </list>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InFlightLoadTable
{
    /// <summary>
    /// 当前调用链上正在执行的回源的标识，最内层在栈顶
    /// </summary>
    /// <remarks>
    /// 各张表共用：标识是唯一的对象，不会认错。只存标识（<see cref="InFlightLoad.Identity"/>），见那里的说明
    /// </remarks>
    private static readonly AsyncLocal<ImmutableStack<object>?> _leading = new();

    /// <summary>
    /// 保护 <see cref="_loads"/>
    /// </summary>
    /// <remarks>
    /// 只在登记、移除与取代时短暂持有，不在持有期间执行回源或等待
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// 键 → 可以加入的在途回源
    /// </summary>
    private readonly Dictionary<string, InFlightLoad> _loads = new(StringComparer.Ordinal);

    /// <summary>
    /// <see cref="_loads"/> 的条数，供不加锁的读取
    /// </summary>
    private volatile int _count;

    /// <summary>
    /// 在途的键数
    /// </summary>
    /// <remarks>
    /// 已被取代而仍在执行的回源不计
    /// </remarks>
    public int Count => _count;

    /// <summary>
    /// 这个键没有在途的回源时执行 <paramref name="load"/>，有则等它的结局
    /// </summary>
    /// <remarks>
    /// <paramref name="load"/> 收到这次回源的句柄与本次调用的 <paramref name="cancellationToken"/>，由它完成「再读一次缓存、未命中才查源、写回」：领头的调用在登记之前读到的未命中可能已经过时（上一次回源刚写回）。
    /// 被合并的调用拿到的是领头那次 <paramref name="load"/> 的结果，所以同一个键必须对应同一份数据
    /// </remarks>
    public async ValueTask<T> RunAsync<T>(CacheKey key, Func<InFlightLoad, CancellationToken, ValueTask<T>> load, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(load);
        var name = key.Value;
        while (true)
        {
            // 已经取消的调用不起回源也不排队
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryJoin<T>(name, out var joined, out var own))
            {
                return await LeadAsync(name, own, load, cancellationToken).ConfigureAwait(false);
            }

            // 只结束自己的等待：领头的回源不受影响
            await joined.Done.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (joined.TryGetValue(out var value))
            {
                return value;
            }

            // 领头的调用取消了：回到开头，先到的接手，其余的等它
        }
    }

    /// <summary>
    /// 这个键被写入或删除了：在途的回源与它脱钩
    /// </summary>
    /// <remarks>
    /// 在改缓存之前调用。脱钩的回源照常结束、结果照常交给已经在等的调用，但不再写回（<see cref="InFlightLoad.IsSuperseded"/>）；之后到达的调用不加入它。没有在途的回源时什么也不做
    /// </remarks>
    public void Supersede(CacheKey key)
    {
        if (_count == 0)
        {
            return;
        }

        InFlightLoad? found;
        lock (_gate)
        {
            if (!_loads.Remove(key.Value, out found))
            {
                return;
            }

            _count = _loads.Count;
        }

        // 表锁之外标记：标记要等这次回源正在进行的写回结束，不能把别的键挡在后面
        found.Supersede();
    }

    /// <summary>
    /// 这些键被写入或删除了：各自在途的回源与它们脱钩
    /// </summary>
    /// <remarks>
    /// 逐键同单键的重载，一次加锁；空集合与重复的键都没有影响
    /// </remarks>
    public void Supersede(IReadOnlyList<CacheKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0 || _count == 0)
        {
            return;
        }

        List<InFlightLoad>? found = null;
        lock (_gate)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                if (_loads.Remove(keys[index].Value, out var load))
                {
                    (found ??= []).Add(load);
                }
            }

            _count = _loads.Count;
        }

        MarkSuperseded(found);
    }

    /// <summary>
    /// 前缀下的键被删除了：这些键在途的回源与它们脱钩
    /// </summary>
    /// <remarks>
    /// 按序数比较；空前缀是全部
    /// </remarks>
    public void SupersedePrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (_count == 0)
        {
            return;
        }

        List<InFlightLoad>? found = null;
        lock (_gate)
        {
            // 表里只有在途的键，扫一遍的代价与同时在回源的调用数成正比
            List<string>? names = null;
            foreach (var (name, load) in _loads)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    (names ??= []).Add(name);
                    (found ??= []).Add(load);
                }
            }

            if (names is not null)
            {
                foreach (var name in names)
                {
                    _loads.Remove(name);
                }

                _count = _loads.Count;
            }
        }

        MarkSuperseded(found);
    }

    /// <summary>
    /// 找到可以加入的回源，没有就为本次调用新建一个
    /// </summary>
    /// <remarks>
    /// 新建的回源在这个键空着时登记进表（别人可以加入）；键上已有不能加入的回源时不登记，本次调用照没有合并时自己回源
    /// </remarks>
    /// <returns>加入了别人的回源为 <see langword="true"/></returns>
    private bool TryJoin<T>(string name, [NotNullWhen(true)] out InFlightLoad<T>? joined, [NotNullWhen(false)] out InFlightLoad<T>? own)
        where T : notnull
    {
        lock (_gate)
        {
            if (!_loads.TryGetValue(name, out var existing))
            {
                joined = null;
                own = new InFlightLoad<T>();
                _loads.Add(name, own);
                _count = _loads.Count;
                return false;
            }

            // 结果类型不同的拿不到它的值；调用链上正在回源的等下去是等自己
            if (existing is InFlightLoad<T> typed && !IsOnCallChain(typed))
            {
                joined = typed;
                own = null;
                return true;
            }

            joined = null;
            own = new InFlightLoad<T>();
            return false;
        }
    }

    /// <summary>
    /// 领头：执行回源并写下结局
    /// </summary>
    private async ValueTask<T> LeadAsync<T>(string name, InFlightLoad<T> flight, Func<InFlightLoad, CancellationToken, ValueTask<T>> load, CancellationToken cancellationToken)
        where T : notnull
    {
        // 记进调用链，回源函数里再读缓存时用来认出自己。本方法是异步方法，这个赋值随它结束而复原，不会漏给调用方
        _leading.Value = (_leading.Value ?? ImmutableStack<object>.Empty).Push(flight.Identity);
        T value;
        try
        {
            // 回源在领头调用自己的上下文里、用它自己的令牌执行：它用到的作用域内的对象（工作单元、连接）只在这次调用里有效
            value = await load(flight, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // 先从表里移除再写结局：等待的调用醒来时这个键已经不在途，重新排的能接手，新来的会重读缓存
            Remove(name, flight);
            if (cancellationToken.IsCancellationRequested)
            {
                // 领头的调用已经取消：不管抛出的是什么都算它自己离开，不让别人替它的取消承担失败
                flight.HandOver();
            }
            else
            {
                // 回源本身失败：这一批等待的调用一起收到，不各自再撞一次
                flight.Fail(exception);
            }

            throw;
        }

        Remove(name, flight);
        flight.Succeed(value);
        return value;
    }

    /// <summary>
    /// 回源结束：从表里移除
    /// </summary>
    /// <remarks>
    /// 被取代的与没有登记的不在表里；同一个键上可能已经是后来的另一次回源，只移除自己
    /// </remarks>
    private void Remove(string name, InFlightLoad flight)
    {
        lock (_gate)
        {
            if (_loads.TryGetValue(name, out var current) && ReferenceEquals(current, flight))
            {
                _loads.Remove(name);
                _count = _loads.Count;
            }
        }
    }

    /// <summary>
    /// 这个回源是否就在当前调用链上执行
    /// </summary>
    private static bool IsOnCallChain(InFlightLoad flight)
    {
        if (_leading.Value is not { } chain)
        {
            return false;
        }

        // 链的深度是回源函数嵌套读缓存的层数，通常是一
        foreach (var identity in chain)
        {
            if (ReferenceEquals(identity, flight.Identity))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把从表里取下的回源标记为已被取代
    /// </summary>
    private static void MarkSuperseded(List<InFlightLoad>? found)
    {
        if (found is null)
        {
            return;
        }

        foreach (var load in found)
        {
            load.Supersede();
        }
    }
}
