// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Models;

/// <summary>
/// 后台作业优先级枚举测试
/// </summary>
/// <remarks>
/// 该枚举会被持久化进作业记录（内存 / Redis 存储都按数值序列化），
/// 且存储端的领取顺序直接依赖数值大小，所以底层类型与每个成员的数值必须锁死。
/// </remarks>
public class BackgroundJobPriorityTests
{
    /// <summary>
    /// 底层类型为 byte，改动会破坏已持久化作业的兼容性
    /// </summary>
    [Fact]
    public void UnderlyingType_IsByte()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(BackgroundJobPriority)));
    }

    /// <summary>
    /// 各优先级数值锁死
    /// </summary>
    /// <param name="priority">优先级</param>
    /// <param name="expected">期望数值</param>
    [Theory]
    [InlineData(BackgroundJobPriority.Low, 5)]
    [InlineData(BackgroundJobPriority.BelowNormal, 10)]
    [InlineData(BackgroundJobPriority.Normal, 15)]
    [InlineData(BackgroundJobPriority.AboveNormal, 20)]
    [InlineData(BackgroundJobPriority.High, 25)]
    public void Values_AreStable(BackgroundJobPriority priority, int expected)
    {
        Assert.Equal(expected, (int)priority);
    }

    /// <summary>
    /// 数值越大越优先：升序排序结果必须是 Low → High
    /// </summary>
    [Fact]
    public void Values_AreOrderedFromLowToHigh()
    {
        var ordered = Enum.GetValues<BackgroundJobPriority>().OrderBy(x => x).ToArray();
        var expected = new[]
        {
            BackgroundJobPriority.Low,
            BackgroundJobPriority.BelowNormal,
            BackgroundJobPriority.Normal,
            BackgroundJobPriority.AboveNormal,
            BackgroundJobPriority.High
        };

        Assert.Equal(expected, ordered);
    }

    /// <summary>
    /// 枚举成员数量固定，新增成员必须同步评估存储与排序影响
    /// </summary>
    [Fact]
    public void Members_CountIsFive()
    {
        Assert.Equal(5, Enum.GetValues<BackgroundJobPriority>().Length);
    }
}
