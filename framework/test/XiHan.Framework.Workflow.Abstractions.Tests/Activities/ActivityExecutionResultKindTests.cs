// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Activities;

/// <summary>
/// 活动执行结果种类枚举测试
/// </summary>
/// <remarks>
/// 该枚举参与节点执行日志与外部编排系统的判定，数值一旦漂移历史数据即被误读，故锁死数值与成员数量。
/// </remarks>
public class ActivityExecutionResultKindTests
{
    /// <summary>
    /// 各成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(ActivityExecutionResultKind.Completed, 1)]
    [InlineData(ActivityExecutionResultKind.Suspended, 2)]
    [InlineData(ActivityExecutionResultKind.Faulted, 3)]
    public void Value_ForEachMember_IsLocked(ActivityExecutionResultKind kind, int expected)
    {
        Assert.Equal(expected, (int)kind);
    }

    /// <summary>
    /// 成员数量锁定，且不存在 0 值成员
    /// </summary>
    /// <remarks>
    /// 刻意从 1 起编号：0 是 default(枚举) 的取值，留空可让"忘记赋值"暴露为非法值而不是静默当成完成。
    /// </remarks>
    [Fact]
    public void Members_Count_IsThreeAndZeroIsUndefined()
    {
        Assert.Equal(3, Enum.GetValues<ActivityExecutionResultKind>().Length);
        Assert.False(Enum.IsDefined((ActivityExecutionResultKind)0));
    }

    /// <summary>
    /// 默认 JSON 序列化输出数值而非名称
    /// </summary>
    [Fact]
    public void JsonSerialize_ByDefault_WritesNumericValue()
    {
        Assert.Equal("2", JsonSerializer.Serialize(ActivityExecutionResultKind.Suspended));
    }
}
