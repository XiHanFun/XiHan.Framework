// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 工作流活动元数据特性测试
/// </summary>
/// <remarks>
/// 引擎按特性上的活动类型编码把流程定义节点解析到具体活动实现，
/// 因此该特性的可发现性（AttributeTargets/AllowMultiple/继承查找）与默认流转行为都是硬契约。
/// </remarks>
public class WorkflowActivityAttributeTests
{
    /// <summary>
    /// 仅传活动类型编码时其余元数据取默认值
    /// </summary>
    [Fact]
    public void Constructor_WithActivityTypeOnly_LeavesOptionalMetadataAtDefaults()
    {
        var attribute = new WorkflowActivityAttribute("CustomActivity");

        Assert.Equal("CustomActivity", attribute.ActivityType);
        Assert.Null(attribute.DisplayName);
        Assert.Null(attribute.Category);
        Assert.Equal(ActivityOutgoingBehavior.AllMatched, attribute.OutgoingBehavior);
    }

    /// <summary>
    /// 活动类型编码只读，构造后不可改写
    /// </summary>
    [Fact]
    public void ActivityType_IsReadOnlyProperty()
    {
        var property = typeof(WorkflowActivityAttribute).GetProperty(nameof(WorkflowActivityAttribute.ActivityType));

        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    /// <summary>
    /// 命名参数写入的元数据可经反射完整读回
    /// </summary>
    [Fact]
    public void GetCustomAttribute_OnDecoratedType_ReturnsAllMetadata()
    {
        var attribute = typeof(SampleDecoratedActivity).GetCustomAttribute<WorkflowActivityAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Sample", attribute.ActivityType);
        Assert.Equal("示例活动", attribute.DisplayName);
        Assert.Equal("测试", attribute.Category);
        Assert.Equal(ActivityOutgoingBehavior.Exclusive, attribute.OutgoingBehavior);
    }

    /// <summary>
    /// 未声明流转行为的活动按 AllMatched 处理
    /// </summary>
    [Fact]
    public void GetCustomAttribute_OnMinimallyDecoratedType_UsesDefaultOutgoingBehavior()
    {
        var attribute = typeof(MinimalDecoratedActivity).GetCustomAttribute<WorkflowActivityAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Minimal", attribute.ActivityType);
        Assert.Equal(ActivityOutgoingBehavior.AllMatched, attribute.OutgoingBehavior);
    }

    /// <summary>
    /// 特性只能标注在类上且不可重复标注
    /// </summary>
    [Fact]
    public void AttributeUsage_TargetsClassAndDisallowsMultiple()
    {
        var usage = typeof(WorkflowActivityAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    /// <summary>
    /// 特性类型封闭
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(WorkflowActivityAttribute).IsSealed);
    }

    /// <summary>
    /// 完整声明元数据的示例活动
    /// </summary>
    [WorkflowActivity("Sample", DisplayName = "示例活动", Category = "测试", OutgoingBehavior = ActivityOutgoingBehavior.Exclusive)]
    private sealed class SampleDecoratedActivity : IWorkflowActivity
    {
        /// <summary>
        /// 执行活动
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <returns>完成结果</returns>
        public Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
        {
            return Task.FromResult(ActivityExecutionResult.Complete());
        }
    }

    /// <summary>
    /// 仅声明活动类型编码的示例活动
    /// </summary>
    [WorkflowActivity("Minimal")]
    private sealed class MinimalDecoratedActivity : IWorkflowActivity
    {
        /// <summary>
        /// 执行活动
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <returns>完成结果</returns>
        public Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
        {
            return Task.FromResult(ActivityExecutionResult.Complete());
        }
    }
}
