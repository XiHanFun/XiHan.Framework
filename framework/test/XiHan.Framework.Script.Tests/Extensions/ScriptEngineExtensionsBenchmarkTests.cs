// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Extensions;
using XiHan.Framework.Script.Tests.Fakes;

namespace XiHan.Framework.Script.Tests.Extensions;

/// <summary>
/// 基准测试统计口径测试
/// </summary>
/// <remarks>
/// 文档约定 <c>BenchmarkAsync</c>「预热 5 次后统计」：耗时与分配量都只能覆盖正式迭代。
/// 用替身引擎按调用序号区分预热与正式执行，让两段执行的耗时、分配量相差一个数量级，
/// 统计窗口一旦混入预热，断言立刻失败。
/// <para>
/// 早先的实现有两处与约定不符：计时起点在预热之前，<c>TotalTimeMs</c> 含预热（首次执行通常还含编译）；
/// 内存取 <c>GC.GetTotalMemory(false)</c> 的堆占用差值，而统计窗口内本身就有一次强制回收，
/// 读数常为零或负数——与 v4.5.0 修掉的 <c>MemoryUsage</c> 是同一个问题。
/// </para>
/// </remarks>
[Collection(ProcessWideMeasurementCollection.Name)]
public class ScriptEngineExtensionsBenchmarkTests
{
    /// <summary>
    /// 文档约定的预热次数
    /// </summary>
    private const int WarmupCount = 5;

    /// <summary>
    /// 预热与正式迭代都会执行，但只有正式迭代计入结果统计
    /// </summary>
    [Fact]
    public async Task BenchmarkAsync_CountsOnlyFormalIterations()
    {
        const int Iterations = 3;
        var engine = new FakeScriptEngine();

        var statistics = await engine.BenchmarkAsync("1 + 1", Iterations);

        Assert.Equal(WarmupCount + Iterations, engine.ExecutedScripts.Count);
        Assert.Equal(Iterations, statistics.TotalIterations);
        Assert.Equal(Iterations, statistics.SuccessCount);
        Assert.Equal(0, statistics.FailureCount);
    }

    /// <summary>
    /// 总耗时只覆盖正式迭代，不含预热
    /// </summary>
    /// <remarks>
    /// 每次预热执行 200 ms、正式执行 50 ms：含预热的实现读数至少 1000 ms，只统计正式迭代的读数约 150 ms。
    /// 上界取预热总耗时减 1，给正式迭代留出八百多毫秒的调度余量；
    /// 下界取正式耗时的一半，确认计时确实覆盖了正式迭代，而不是一个空窗口。
    /// </remarks>
    [Fact]
    public async Task BenchmarkAsync_TotalTimeMs_ExcludesWarmup()
    {
        const int WarmupDelayMs = 200;
        const int FormalDelayMs = 50;
        const int Iterations = 3;

        var engine = new FakeScriptEngine();
        engine.ExecuteHandler = (code, _) =>
        {
            // ExecutedScripts 在调用处理器之前已记录本次执行，Count 即本次是第几次执行
            Thread.Sleep(engine.ExecutedScripts.Count <= WarmupCount ? WarmupDelayMs : FormalDelayMs);
            return ScriptResult.Success(code);
        };

        var statistics = await engine.BenchmarkAsync("1 + 1", Iterations);

        Assert.InRange(statistics.TotalTimeMs, FormalDelayMs * Iterations / 2, (WarmupDelayMs * WarmupCount) - 1);
    }

    /// <summary>
    /// 正式迭代期间的分配即使已被回收，也计入内存统计
    /// </summary>
    /// <remarks>
    /// 每次正式执行分配一块大于大对象堆阈值的垃圾，最后一次执行结束前强制阻塞回收，垃圾必然被释放。
    /// 分配量口径如实计入这些分配；堆占用差值口径在回收后归零甚至为负，把实际消耗记成零消耗。
    /// 断言门槛取实际分配量的一半，理由同 <see cref="Core.MemoryUsageTests.AllocatedBytes_CountsCollectedGarbage"/>。
    /// </remarks>
    [Fact]
    public async Task BenchmarkAsync_MemoryUsageBytes_CountsAllocationsReclaimedByGc()
    {
        const int BlockSize = 512 * 1024;
        const int Iterations = 16;

        var engine = new FakeScriptEngine();
        engine.ExecuteHandler = (code, _) =>
        {
            var executionNumber = engine.ExecutedScripts.Count;
            if (executionNumber > WarmupCount)
            {
                GC.KeepAlive(new byte[BlockSize]);
            }

            if (executionNumber == WarmupCount + Iterations)
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }

            return ScriptResult.Success(code);
        };

        var statistics = await engine.BenchmarkAsync("1 + 1", Iterations);

        Assert.True(statistics.MemoryUsageBytes >= (long)BlockSize * Iterations / 2,
            $"正式迭代分配了 {(long)BlockSize * Iterations} 字节，统计值为 {statistics.MemoryUsageBytes}");
    }

    /// <summary>
    /// 预热期间的分配不计入内存统计，且正式迭代没有额外分配时读数不为负
    /// </summary>
    /// <remarks>
    /// 预热每次分配 8 MB 垃圾，正式执行不做额外分配。上界取单次预热的分配量：
    /// 统计窗口哪怕只混入一次预热，读数也会越过它；替身引擎自身的簿记分配只有若干 KB，
    /// 本集合又禁用了并行，其他用例的分配不会把读数推过上界。
    /// </remarks>
    [Fact]
    public async Task BenchmarkAsync_MemoryUsageBytes_ExcludesWarmupAndIsNeverNegative()
    {
        const int WarmupBlockSize = 8 * 1024 * 1024;

        var engine = new FakeScriptEngine();
        engine.ExecuteHandler = (code, _) =>
        {
            if (engine.ExecutedScripts.Count <= WarmupCount)
            {
                GC.KeepAlive(new byte[WarmupBlockSize]);
            }

            return ScriptResult.Success(code);
        };

        var statistics = await engine.BenchmarkAsync("1 + 1", 10);

        Assert.InRange(statistics.MemoryUsageBytes, 0L, WarmupBlockSize - 1L);
    }
}
