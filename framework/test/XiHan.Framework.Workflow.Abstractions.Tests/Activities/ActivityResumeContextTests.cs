// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 活动恢复上下文测试
/// </summary>
/// <remarks>
/// 恢复上下文继承执行上下文，这一继承关系是引擎能把恢复分支与执行分支复用同一套活动 API 的前提。
/// </remarks>
public class ActivityResumeContextTests
{
    /// <summary>
    /// 恢复上下文可当作执行上下文使用
    /// </summary>
    [Fact]
    public void Type_DerivesFromExecutionContext()
    {
        ActivityExecutionContext context = WorkflowTestModels.CreateResumeContext();

        Assert.True(typeof(ActivityExecutionContext).IsAssignableFrom(typeof(ActivityResumeContext)));
        Assert.Equal("ins-1", context.Instance.Id);
        Assert.Equal("start", context.Node.Id);
    }

    /// <summary>
    /// 未显式赋值时恢复输入为空字典而非 null
    /// </summary>
    [Fact]
    public void Inputs_WhenNotAssigned_IsEmptyDictionary()
    {
        var context = WorkflowTestModels.CreateResumeContext();

        Assert.NotNull(context.Inputs);
        Assert.Empty(context.Inputs);
    }

    /// <summary>
    /// 恢复输入原样可读
    /// </summary>
    [Fact]
    public void Inputs_WhenAssigned_KeepsValues()
    {
        var inputs = new Dictionary<string, object?>
        {
            [WorkflowConsts.OutcomeVariableName] = WorkflowUserTaskOutcomes.Approved,
            ["comment"] = "同意"
        };

        var context = WorkflowTestModels.CreateResumeContext(inputs);

        Assert.Equal(2, context.Inputs.Count);
        Assert.Equal("approved", context.Inputs[WorkflowConsts.OutcomeVariableName]);
        Assert.Equal("同意", context.Inputs["comment"]);
    }

    /// <summary>
    /// 被消费的书签原样透出
    /// </summary>
    [Fact]
    public void Bookmark_IsExposedWithKindAndKey()
    {
        var context = WorkflowTestModels.CreateResumeContext();

        Assert.Equal("bm-1", context.Bookmark.Id);
        Assert.Equal(WorkflowBookmarkKinds.UserTask, context.Bookmark.Kind);
        Assert.Equal("u-1", context.Bookmark.Key);
        Assert.Equal("ni-1", context.Bookmark.NodeInstanceId);
    }
}
