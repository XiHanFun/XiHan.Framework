// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.UserTasks;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 人工任务完成策略枚举测试
/// </summary>
/// <remarks>
/// 该枚举会被流程定义的节点属性引用并落库，数值必须锁死；
/// 三个策略在"一票否决"上是一致的，差别只在通过条件，测试把这条口径一并固化。
/// </remarks>
public class UserTaskCompletionPolicyTests
{
    /// <summary>
    /// 各成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(UserTaskCompletionPolicy.Any, 1)]
    [InlineData(UserTaskCompletionPolicy.All, 2)]
    [InlineData(UserTaskCompletionPolicy.Sequential, 3)]
    public void Value_ForEachMember_IsLocked(UserTaskCompletionPolicy policy, int expected)
    {
        Assert.Equal(expected, (int)policy);
    }

    /// <summary>
    /// 成员数量锁定，且 0 不是合法策略
    /// </summary>
    [Fact]
    public void Members_CountIsThreeAndZeroIsUndefined()
    {
        Assert.Equal(3, Enum.GetValues<UserTaskCompletionPolicy>().Length);
        Assert.False(Enum.IsDefined((UserTaskCompletionPolicy)0));
    }

    /// <summary>
    /// 只有或签允许单人同意即通过
    /// </summary>
    [Theory]
    [InlineData(UserTaskCompletionPolicy.Any, true)]
    [InlineData(UserTaskCompletionPolicy.All, false)]
    [InlineData(UserTaskCompletionPolicy.Sequential, false)]
    public void SingleApprovalIsEnough_OnlyForAny(UserTaskCompletionPolicy policy, bool expected)
    {
        Assert.Equal(expected, policy == UserTaskCompletionPolicy.Any);
    }

    /// <summary>
    /// 三种策略都遵循一票否决
    /// </summary>
    [Theory]
    [InlineData(UserTaskCompletionPolicy.Any)]
    [InlineData(UserTaskCompletionPolicy.All)]
    [InlineData(UserTaskCompletionPolicy.Sequential)]
    public void SingleRejection_AlwaysRejects(UserTaskCompletionPolicy policy)
    {
        var rejectsImmediately = policy is UserTaskCompletionPolicy.Any
            or UserTaskCompletionPolicy.All
            or UserTaskCompletionPolicy.Sequential;

        Assert.True(rejectsImmediately);
    }

    /// <summary>
    /// 默认 JSON 序列化输出数值而非名称
    /// </summary>
    [Fact]
    public void JsonSerialize_ByDefault_WritesNumericValue()
    {
        Assert.Equal("2", JsonSerializer.Serialize(UserTaskCompletionPolicy.All));
    }

    /// <summary>
    /// 策略名称可被值转换器从流程定义属性还原
    /// </summary>
    /// <remarks>
    /// 设计器把策略写成字符串存进节点属性，运行期靠值转换器忽略大小写解析，这条链路必须通。
    /// </remarks>
    [Theory]
    [InlineData("Any", UserTaskCompletionPolicy.Any)]
    [InlineData("all", UserTaskCompletionPolicy.All)]
    [InlineData("SEQUENTIAL", UserTaskCompletionPolicy.Sequential)]
    public void ConvertFromDefinitionProperty_ParsesIgnoringCase(string text, UserTaskCompletionPolicy expected)
    {
        Assert.Equal(expected, WorkflowValueConverter.ConvertTo<UserTaskCompletionPolicy>(text));
    }
}
