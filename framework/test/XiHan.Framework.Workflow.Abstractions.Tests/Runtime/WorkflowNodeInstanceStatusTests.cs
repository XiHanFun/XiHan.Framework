// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Runtime;

/// <summary>
/// 节点实例状态枚举测试
/// </summary>
/// <remarks>
/// 与实例状态编号对齐（1..6）但第 6 位语义不同：实例是 Terminated，节点是 Compensated。
/// 两者数值相同、含义不同，最容易被"照着改"改错，故显式锁死。
/// </remarks>
public class WorkflowNodeInstanceStatusTests
{
    /// <summary>
    /// 各成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(WorkflowNodeInstanceStatus.Running, 1)]
    [InlineData(WorkflowNodeInstanceStatus.Suspended, 2)]
    [InlineData(WorkflowNodeInstanceStatus.Completed, 3)]
    [InlineData(WorkflowNodeInstanceStatus.Canceled, 4)]
    [InlineData(WorkflowNodeInstanceStatus.Faulted, 5)]
    [InlineData(WorkflowNodeInstanceStatus.Compensated, 6)]
    public void Value_ForEachMember_IsLocked(WorkflowNodeInstanceStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    /// <summary>
    /// 成员数量锁定，且 0 不是合法状态
    /// </summary>
    [Fact]
    public void Members_CountIsSixAndZeroIsUndefined()
    {
        Assert.Equal(6, Enum.GetValues<WorkflowNodeInstanceStatus>().Length);
        Assert.False(Enum.IsDefined((WorkflowNodeInstanceStatus)0));
    }

    /// <summary>
    /// 补偿态是节点独有语义，不与实例的终止态混淆
    /// </summary>
    [Fact]
    public void Compensated_IsNodeOnlySemantic()
    {
        Assert.Equal(6, (int)WorkflowNodeInstanceStatus.Compensated);
        Assert.False(Enum.GetNames<WorkflowNodeInstanceStatus>().Contains("Terminated"));
    }

    /// <summary>
    /// 默认 JSON 序列化输出数值而非名称
    /// </summary>
    [Fact]
    public void JsonSerialize_ByDefault_WritesNumericValue()
    {
        Assert.Equal("6", JsonSerializer.Serialize(WorkflowNodeInstanceStatus.Compensated));
    }
}
