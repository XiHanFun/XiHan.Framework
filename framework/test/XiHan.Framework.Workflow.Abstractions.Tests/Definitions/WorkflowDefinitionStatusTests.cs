// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程定义状态枚举测试
/// </summary>
/// <remarks>
/// 该枚举直接落库，数值即历史数据的含义，必须锁死；
/// 同时锁定"新建定义默认草稿"这一入口语义（0 值必须是 Draft）。
/// </remarks>
public class WorkflowDefinitionStatusTests
{
    /// <summary>
    /// 各成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(WorkflowDefinitionStatus.Draft, 0)]
    [InlineData(WorkflowDefinitionStatus.Published, 1)]
    [InlineData(WorkflowDefinitionStatus.Disabled, 2)]
    [InlineData(WorkflowDefinitionStatus.Archived, 3)]
    public void Value_ForEachMember_IsLocked(WorkflowDefinitionStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    /// <summary>
    /// 枚举默认值为草稿
    /// </summary>
    [Fact]
    public void Default_IsDraft()
    {
        Assert.Equal(WorkflowDefinitionStatus.Draft, default(WorkflowDefinitionStatus));
        Assert.Equal(WorkflowDefinitionStatus.Draft, new WorkflowDefinition().Status);
    }

    /// <summary>
    /// 成员数量锁定
    /// </summary>
    [Fact]
    public void Members_Count_IsFour()
    {
        Assert.Equal(4, Enum.GetValues<WorkflowDefinitionStatus>().Length);
        Assert.False(Enum.IsDefined((WorkflowDefinitionStatus)4));
    }

    /// <summary>
    /// 仅已发布状态允许启动新实例
    /// </summary>
    /// <remarks>
    /// 用状态集合表达"可启动"这一业务口径，把它固定在抽象层测试里，
    /// 后续新增状态（如"审批中"）时必须显式决定它是否可启动，而不是靠默认行为漏进来。
    /// </remarks>
    [Theory]
    [InlineData(WorkflowDefinitionStatus.Draft, false)]
    [InlineData(WorkflowDefinitionStatus.Published, true)]
    [InlineData(WorkflowDefinitionStatus.Disabled, false)]
    [InlineData(WorkflowDefinitionStatus.Archived, false)]
    public void Startable_OnlyForPublished(WorkflowDefinitionStatus status, bool expected)
    {
        Assert.Equal(expected, status == WorkflowDefinitionStatus.Published);
    }

    /// <summary>
    /// 默认 JSON 序列化输出数值而非名称
    /// </summary>
    [Fact]
    public void JsonSerialize_ByDefault_WritesNumericValue()
    {
        Assert.Equal("1", JsonSerializer.Serialize(WorkflowDefinitionStatus.Published));
        Assert.Equal("3", JsonSerializer.Serialize(WorkflowDefinitionStatus.Archived));
    }
}
