// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务统计信息的处理时间样本淘汰测试
/// </summary>
/// <remarks>
/// 平均处理时间的口径是"最近 1000 个任务"，这决定了它是一条能反映当下的曲线，
/// 而不是掺着任意时间窗口旧样本的糊涂账。淘汰一旦不按插入次序走，口径就不成立——
/// 而这件事在样本数没过上限时完全看不出来，必须把样本压过 1000 才能暴露。
/// <para>
/// 用例把每个样本的耗时设成可辨认的值，直接用平均值反推"留下来的到底是哪一批样本"，
/// 而不是去断言内部容器的大小。
/// </para>
/// </remarks>
public class BackgroundServiceStatisticsSampleEvictionTests
{
    /// <summary>
    /// 处理时间样本上限（与实现保持一致）
    /// </summary>
    private const int SampleCap = 1000;

    /// <summary>
    /// 恰好装满上限时一个样本都不淘汰
    /// </summary>
    [Fact]
    public void RecordTaskCompleted_AtExactlyCap_KeepsEverySample()
    {
        var statistics = new BackgroundServiceStatistics();

        // 样本值取 0..999，平均值 499.5 唯一对应"一个都没少"
        for (var i = 0; i < SampleCap; i++)
        {
            statistics.RecordTaskCompleted($"s{i}", i, true);
        }

        Assert.Equal(499.5d, statistics.AverageProcessingTimeMs, 6);
    }

    /// <summary>
    /// 超出上限时淘汰的是最先记录的那一条，而不是任意一条
    /// </summary>
    /// <remarks>
    /// 第 1001 条进来时应当挤掉 s0（样本值 0），平均值随之变成 (499500 - 0 + 100000) / 1000 = 599.5。
    /// 若淘汰的是别的样本，被减掉的就不是 0，平均值必然对不上。
    /// </remarks>
    [Fact]
    public void RecordTaskCompleted_WhenCapExceeded_EvictsTheOldestSample()
    {
        var statistics = new BackgroundServiceStatistics();

        for (var i = 0; i < SampleCap; i++)
        {
            statistics.RecordTaskCompleted($"s{i}", i, true);
        }

        statistics.RecordTaskCompleted("overflow", 100_000, true);

        Assert.Equal(599.5d, statistics.AverageProcessingTimeMs, 6);
    }

    /// <summary>
    /// 整整一代样本被换掉之后，留下的必须全是新样本
    /// </summary>
    /// <remarks>
    /// 这是"最近 1000 个"最直接的表达：先灌 1000 条旧样本（耗时 100），再灌 1000 条新样本（耗时 200），
    /// 正确淘汰下平均值只能是 200。按任意顺序淘汰时新旧样本必然混着留，平均值会落在两者之间。
    /// </remarks>
    [Fact]
    public void RecordTaskCompleted_WhenAWholeGenerationIsReplaced_KeepsOnlyTheNewest()
    {
        var statistics = new BackgroundServiceStatistics();

        for (var i = 0; i < SampleCap; i++)
        {
            statistics.RecordTaskCompleted($"old-{i}", 100, true);
        }

        for (var i = 0; i < SampleCap; i++)
        {
            statistics.RecordTaskCompleted($"new-{i}", 200, true);
        }

        Assert.Equal(200d, statistics.AverageProcessingTimeMs, 6);
    }

    /// <summary>
    /// 边界：同一任务标识重复上报不占额外名额，也不会提前挤掉仍在用的样本
    /// </summary>
    /// <remarks>
    /// 耗时样本按标识去重（首次记录为准，见 <c>BackgroundServiceStatisticsTests</c>），
    /// 因此重复上报既不该让样本数变多，也不该在淘汰次序里多占位置。
    /// 这里先用重复上报把 dup 这一条钉在最旧的位置，再补满到上限、压出一次淘汰：
    /// 被挤掉的应当正好是 dup，平均值 (999 × 100 + 1100) / 1000 = 101。
    /// </remarks>
    [Fact]
    public void RecordTaskCompleted_WhenSameTaskIdRepeats_DoesNotConsumeExtraSampleSlots()
    {
        var statistics = new BackgroundServiceStatistics();

        statistics.RecordTaskCompleted("dup", 100, true);
        for (var i = 0; i < 200; i++)
        {
            statistics.RecordTaskCompleted("dup", 999, true);
        }

        for (var i = 1; i < SampleCap; i++)
        {
            statistics.RecordTaskCompleted($"s{i}", 100, true);
        }

        // 此刻恰好 1000 个样本，全部是 100
        Assert.Equal(100d, statistics.AverageProcessingTimeMs, 6);

        statistics.RecordTaskCompleted("overflow", 1_100, true);

        Assert.Equal(101d, statistics.AverageProcessingTimeMs, 6);
    }

    /// <summary>
    /// 反例：重置之后淘汰次序也跟着清空，新一批样本从零开始计
    /// </summary>
    [Fact]
    public void Reset_ThenRecordAgain_StartsSampleWindowOver()
    {
        var statistics = new BackgroundServiceStatistics();

        for (var i = 0; i < SampleCap + 200; i++)
        {
            statistics.RecordTaskCompleted($"before-{i}", 100, true);
        }

        statistics.Reset();

        statistics.RecordTaskCompleted("after-1", 10, true);
        statistics.RecordTaskCompleted("after-2", 20, true);
        statistics.RecordTaskCompleted("after-3", 30, true);

        Assert.Equal(20d, statistics.AverageProcessingTimeMs, 6);
        Assert.Equal(3, statistics.TotalTasksProcessed);
    }
}
