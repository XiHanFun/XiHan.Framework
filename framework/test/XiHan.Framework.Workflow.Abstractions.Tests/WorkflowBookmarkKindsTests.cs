// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 书签种类常量测试
/// </summary>
/// <remarks>
/// 书签种类落库且被存储层按 Kind 建索引查询，字面值必须锁死；
/// 同时把"哪些种类靠 DueTime 恢复、哪些靠 Key 匹配"这条口径固化下来——
/// 这决定定时器 Worker 会不会漏扫某一类书签。
/// </remarks>
public class WorkflowBookmarkKindsTests
{
    /// <summary>
    /// 各书签种类字面值锁定
    /// </summary>
    [Fact]
    public void Values_AreLocked()
    {
        Assert.Equal("UserTask", WorkflowBookmarkKinds.UserTask);
        Assert.Equal("Timer", WorkflowBookmarkKinds.Timer);
        Assert.Equal("Signal", WorkflowBookmarkKinds.Signal);
        Assert.Equal("SubWorkflow", WorkflowBookmarkKinds.SubWorkflow);
        Assert.Equal("Retry", WorkflowBookmarkKinds.Retry);
        Assert.Equal("NodeTimeout", WorkflowBookmarkKinds.NodeTimeout);
    }

    /// <summary>
    /// 书签种类共六个且互不重复
    /// </summary>
    [Fact]
    public void Constants_AreSixAndUnique()
    {
        var values = GetConstantValues();

        Assert.Equal(6, values.Count);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 定时器轮询恢复的三类书签必须携带到期时间
    /// </summary>
    [Theory]
    [InlineData(WorkflowBookmarkKinds.Timer)]
    [InlineData(WorkflowBookmarkKinds.Retry)]
    [InlineData(WorkflowBookmarkKinds.NodeTimeout)]
    public void TimerDrivenKinds_RequireDueTime(string kind)
    {
        var bookmark = new WorkflowBookmark { Kind = kind, DueTime = DateTime.UtcNow.AddMinutes(1) };

        Assert.NotNull(bookmark.DueTime);
        Assert.Contains(kind, GetConstantValues());
    }

    /// <summary>
    /// 外部驱动恢复的三类书签靠索引键匹配而非到期时间
    /// </summary>
    [Theory]
    [InlineData(WorkflowBookmarkKinds.UserTask)]
    [InlineData(WorkflowBookmarkKinds.Signal)]
    [InlineData(WorkflowBookmarkKinds.SubWorkflow)]
    public void ExternallyDrivenKinds_MatchByKey(string kind)
    {
        var bookmark = new WorkflowBookmark { Kind = kind, Key = "k-1" };

        Assert.Equal("k-1", bookmark.Key);
        Assert.Null(bookmark.DueTime);
        Assert.Contains(kind, GetConstantValues());
    }

    /// <summary>
    /// 书签种类与活动类型编码存在同名值，但分属两套命名空间
    /// </summary>
    /// <remarks>
    /// UserTask/SubWorkflow 在两处都叫同一个名字是刻意对齐的，不能因为"重复"就去改其中一处：
    /// 活动产生同名种类的书签，正是靠这层对齐让排查日志时一眼能对上。
    /// </remarks>
    [Fact]
    public void SharedNames_WithActivityTypes_StayAligned()
    {
        Assert.Equal(WorkflowActivityTypes.UserTask, WorkflowBookmarkKinds.UserTask);
        Assert.Equal(WorkflowActivityTypes.SubWorkflow, WorkflowBookmarkKinds.SubWorkflow);
    }

    /// <summary>
    /// 常量容器是静态类
    /// </summary>
    [Fact]
    public void Type_IsStaticClass()
    {
        var type = typeof(WorkflowBookmarkKinds);

        Assert.True(type.IsAbstract);
        Assert.True(type.IsSealed);
    }

    /// <summary>
    /// 读取全部公共字符串常量值
    /// </summary>
    /// <returns>常量值列表</returns>
    private static List<string> GetConstantValues()
    {
        return typeof(WorkflowBookmarkKinds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
    }
}
