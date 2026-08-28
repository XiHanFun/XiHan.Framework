// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Activities;

/// <summary>
/// 活动出边流转行为枚举测试
/// </summary>
/// <remarks>
/// 该枚举由 <see cref="WorkflowActivityAttribute"/> 声明，0 值必须是默认流转语义 AllMatched：
/// 未显式声明流转行为的自定义活动依赖这一点。
/// </remarks>
public class ActivityOutgoingBehaviorTests
{
    /// <summary>
    /// 各成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(ActivityOutgoingBehavior.AllMatched, 0)]
    [InlineData(ActivityOutgoingBehavior.Exclusive, 1)]
    [InlineData(ActivityOutgoingBehavior.All, 2)]
    [InlineData(ActivityOutgoingBehavior.None, 3)]
    public void Value_ForEachMember_IsLocked(ActivityOutgoingBehavior behavior, int expected)
    {
        Assert.Equal(expected, (int)behavior);
    }

    /// <summary>
    /// 枚举默认值就是按条件流转
    /// </summary>
    [Fact]
    public void Default_IsAllMatched()
    {
        Assert.Equal(ActivityOutgoingBehavior.AllMatched, default(ActivityOutgoingBehavior));
    }

    /// <summary>
    /// 成员数量锁定
    /// </summary>
    [Fact]
    public void Members_Count_IsFour()
    {
        Assert.Equal(4, Enum.GetValues<ActivityOutgoingBehavior>().Length);
    }
}
