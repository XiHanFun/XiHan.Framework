// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程实例状态枚举测试
/// </summary>
/// <remarks>
/// 实例状态直接落库并被子流程回调、监控看板读取，数值必须锁死；
/// 从 1 起编号是刻意设计——0 不是合法状态，可让未初始化的实例暴露出来而不是被当成运行中。
/// </remarks>
public class WorkflowInstanceStatusTests
{
    /// <summary>
    /// 各成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(WorkflowInstanceStatus.Running, 1)]
    [InlineData(WorkflowInstanceStatus.Suspended, 2)]
    [InlineData(WorkflowInstanceStatus.Completed, 3)]
    [InlineData(WorkflowInstanceStatus.Canceled, 4)]
    [InlineData(WorkflowInstanceStatus.Faulted, 5)]
    [InlineData(WorkflowInstanceStatus.Terminated, 6)]
    public void Value_ForEachMember_IsLocked(WorkflowInstanceStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    /// <summary>
    /// 成员数量锁定，且 0 不是合法状态
    /// </summary>
    [Fact]
    public void Members_CountIsSixAndZeroIsUndefined()
    {
        Assert.Equal(6, Enum.GetValues<WorkflowInstanceStatus>().Length);
        Assert.False(Enum.IsDefined((WorkflowInstanceStatus)0));
        Assert.False(Enum.IsDefined((WorkflowInstanceStatus)7));
    }

    /// <summary>
    /// 新建实例的状态默认是运行中而不是枚举默认值
    /// </summary>
    /// <remarks>
    /// 实例模型上的初始化器显式写了 Running，与枚举默认值 0 无关，两者不能互相顶替。
    /// </remarks>
    [Fact]
    public void NewInstance_Status_IsRunningNotEnumDefault()
    {
        Assert.Equal(WorkflowInstanceStatus.Running, new WorkflowInstance().Status);
        Assert.NotEqual(WorkflowInstanceStatus.Running, default(WorkflowInstanceStatus));
    }

    /// <summary>
    /// 默认 JSON 序列化输出数值而非名称
    /// </summary>
    [Fact]
    public void JsonSerialize_ByDefault_WritesNumericValue()
    {
        Assert.Equal("3", JsonSerializer.Serialize(WorkflowInstanceStatus.Completed));
        Assert.Equal("6", JsonSerializer.Serialize(WorkflowInstanceStatus.Terminated));
    }

    /// <summary>
    /// 可恢复运行的状态只有挂起与故障
    /// </summary>
    /// <remarks>
    /// 恢复运行走 ResumeAsync（挂起）、重试走 RetryAsync（故障），其余状态都不该有恢复入口。
    /// 把这条口径固化下来，新增状态时必须显式回答"它能不能被恢复"。
    /// </remarks>
    [Theory]
    [InlineData(WorkflowInstanceStatus.Running, false)]
    [InlineData(WorkflowInstanceStatus.Suspended, true)]
    [InlineData(WorkflowInstanceStatus.Completed, false)]
    [InlineData(WorkflowInstanceStatus.Canceled, false)]
    [InlineData(WorkflowInstanceStatus.Faulted, true)]
    [InlineData(WorkflowInstanceStatus.Terminated, false)]
    public void Recoverable_OnlyForSuspendedAndFaulted(WorkflowInstanceStatus status, bool expected)
    {
        var recoverable = status is WorkflowInstanceStatus.Suspended or WorkflowInstanceStatus.Faulted;

        Assert.Equal(expected, recoverable);
    }
}
