// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.Persistence.Conformance;

/// <summary>
/// <see cref="IIdGenerator{TKey}"/> 的一致性套件。
/// </summary>
/// <remarks>
/// 顺序生成与多线程并发生成不重复，时钟前进后生成的标识更大。继承并实现 <see cref="CreateGenerator"/>，全部用例随之执行。唯一性用例用真实时钟（生成器可以按毫秒限速），顺序用例用可拨动的 <see cref="ConformanceClock"/>。
/// </remarks>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class IdGeneratorConformance<TKey>
    where TKey : notnull, IComparable<TKey>
{
    /// <summary>
    /// 顺序用例生成的个数。
    /// </summary>
    private const int SequentialCount = 10_000;

    /// <summary>
    /// 并发用例的线程数。
    /// </summary>
    private const int ThreadCount = 8;

    /// <summary>
    /// 并发用例每个线程生成的个数。
    /// </summary>
    private const int CountPerThread = 2_000;

    /// <summary>
    /// 创建被测生成器。
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次。
    /// </remarks>
    /// <param name="clock">生成器应使用的时钟：唯一性用例传系统时钟，顺序用例传可拨动的时钟。</param>
    protected abstract IIdGenerator<TKey> CreateGenerator(TimeProvider clock);

    /// <summary>
    /// 用例 1：同一线程连续生成的标识两两不同。
    /// </summary>
    [Fact]
    public void NewId_IsUniqueAcrossSequentialCalls()
    {
        var generator = CreateGenerator(TimeProvider.System);
        var ids = new HashSet<TKey>();
        for (var i = 0; i < SequentialCount; i++)
        {
            Assert.True(ids.Add(generator.NewId()), $"The generator produced a duplicate ID after {i} IDs.");
        }
    }

    /// <summary>
    /// 用例 2：多个线程同时生成的标识全部不同。
    /// </summary>
    [Fact]
    public async Task NewId_IsUniqueAcrossThreads()
    {
        var generator = CreateGenerator(TimeProvider.System);
        var ids = new ConcurrentBag<TKey>();
        using var start = new ManualResetEventSlim(false);
        var workers = Enumerable.Range(0, ThreadCount).Select(_ => Task.Run(() =>
        {
            // 所有线程等同一个信号再开始：让它们真正同时竞争同一个生成器
            start.Wait();
            for (var i = 0; i < CountPerThread; i++)
            {
                ids.Add(generator.NewId());
            }
        })).ToArray();
        start.Set();
        await Task.WhenAll(workers).ConfigureAwait(false);

        Assert.Equal(ThreadCount * CountPerThread, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    /// 用例 3：时钟前进后生成的标识大于之前的（时间有序）。
    /// </summary>
    [Fact]
    public void NewId_GrowsWithClock()
    {
        var clock = new ConformanceClock(RepositoryConformance.Epoch);
        var generator = CreateGenerator(clock);
        var first = generator.NewId();
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = generator.NewId();
        clock.Advance(TimeSpan.FromDays(1));
        var third = generator.NewId();

        Assert.True(second.CompareTo(first) > 0, $"'{second}' should be greater than '{first}'.");
        Assert.True(third.CompareTo(second) > 0, $"'{third}' should be greater than '{second}'.");
    }
}
