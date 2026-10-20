// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.UserTasks;

namespace XiHan.Framework.Workflow.Abstractions.Tests.UserTasks;

/// <summary>
/// 受理人为空策略枚举测试
/// </summary>
/// <remarks>
/// 策略以成员名写入节点属性 <c>EmptyAssigneeStrategy</c> 并落库，成员名与数值都必须锁死；
/// 未配置时等价于 Fault，这一默认口径由活动实现保证，这里只锁枚举本身。
/// </remarks>
public class UserTaskEmptyAssigneeStrategyTests
{
    /// <summary>
    /// 各成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(UserTaskEmptyAssigneeStrategy.Fault, 1)]
    [InlineData(UserTaskEmptyAssigneeStrategy.AutoApprove, 2)]
    [InlineData(UserTaskEmptyAssigneeStrategy.AutoReject, 3)]
    [InlineData(UserTaskEmptyAssigneeStrategy.Fallback, 4)]
    public void Value_ForEachMember_IsLocked(UserTaskEmptyAssigneeStrategy strategy, int expected)
    {
        Assert.Equal(expected, (int)strategy);
    }

    /// <summary>
    /// 成员数量锁定，且 0 不是合法策略
    /// </summary>
    [Fact]
    public void Members_CountIsFourAndZeroIsUndefined()
    {
        Assert.Equal(4, Enum.GetValues<UserTaskEmptyAssigneeStrategy>().Length);
        Assert.False(Enum.IsDefined((UserTaskEmptyAssigneeStrategy)0));
    }

    /// <summary>
    /// 成员名大小写不敏感可解析（节点属性按名称写入）
    /// </summary>
    [Theory]
    [InlineData("fault", UserTaskEmptyAssigneeStrategy.Fault)]
    [InlineData("AutoApprove", UserTaskEmptyAssigneeStrategy.AutoApprove)]
    [InlineData("autoreject", UserTaskEmptyAssigneeStrategy.AutoReject)]
    [InlineData("FALLBACK", UserTaskEmptyAssigneeStrategy.Fallback)]
    public void Parse_ByName_IgnoresCase(string text, UserTaskEmptyAssigneeStrategy expected)
    {
        Assert.True(Enum.TryParse<UserTaskEmptyAssigneeStrategy>(text, ignoreCase: true, out var parsed));
        Assert.Equal(expected, parsed);
    }
}
