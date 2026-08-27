// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 工作流活动接口族测试
/// </summary>
/// <remarks>
/// 引擎按 <see cref="IWorkflowActivity"/> 统一解析活动，再用 is 判断是否支持恢复与补偿；
/// 因此"可恢复/可补偿接口继承自基础活动接口"是引擎分支判断的前提，必须锁死。
/// 这里用最小手写实现验证接口可实现、可分派，不依赖任何真实活动。
/// </remarks>
public class WorkflowActivityInterfacesTests
{
    /// <summary>
    /// 可恢复活动接口继承自基础活动接口
    /// </summary>
    [Fact]
    public void ResumableActivity_Extends_WorkflowActivity()
    {
        Assert.True(typeof(IWorkflowActivity).IsAssignableFrom(typeof(IResumableWorkflowActivity)));
    }

    /// <summary>
    /// 可补偿活动接口继承自基础活动接口
    /// </summary>
    [Fact]
    public void CompensableActivity_Extends_WorkflowActivity()
    {
        Assert.True(typeof(IWorkflowActivity).IsAssignableFrom(typeof(ICompensableWorkflowActivity)));
    }

    /// <summary>
    /// 可恢复与可补偿之间互不继承，允许活动只实现其中之一
    /// </summary>
    [Fact]
    public void ResumableAndCompensable_AreIndependentInterfaces()
    {
        Assert.False(typeof(IResumableWorkflowActivity).IsAssignableFrom(typeof(ICompensableWorkflowActivity)));
        Assert.False(typeof(ICompensableWorkflowActivity).IsAssignableFrom(typeof(IResumableWorkflowActivity)));
    }

    /// <summary>
    /// 仅实现基础接口的活动不会被误判为可恢复或可补偿
    /// </summary>
    [Fact]
    public void PlainActivity_IsNotResumableOrCompensable()
    {
        IWorkflowActivity activity = new PlainActivity();

        Assert.False(activity is IResumableWorkflowActivity);
        Assert.False(activity is ICompensableWorkflowActivity);
    }

    /// <summary>
    /// 基础活动经接口分派后返回完成结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ThroughBaseInterface_ReturnsCompleted()
    {
        IWorkflowActivity activity = new PlainActivity();

        var result = await activity.ExecuteAsync(WorkflowTestModels.CreateExecutionContext());

        Assert.Equal(ActivityExecutionResultKind.Completed, result.Kind);
        Assert.Equal("plain", result.Outcome);
    }

    /// <summary>
    /// 可恢复活动经恢复上下文分派，恢复输入可读
    /// </summary>
    [Fact]
    public async Task ResumeAsync_WithInputs_MergesInputsIntoOutputs()
    {
        var activity = new RecordingResumableActivity();
        var context = WorkflowTestModels.CreateResumeContext(new Dictionary<string, object?> { ["comment"] = "同意" });

        var result = await activity.ResumeAsync(context);

        Assert.Equal(ActivityExecutionResultKind.Completed, result.Kind);
        Assert.Equal("同意", result.Outputs["comment"]);
        Assert.Equal(WorkflowBookmarkKinds.UserTask, activity.ResumedBookmarkKind);
    }

    /// <summary>
    /// 可恢复活动先挂起再恢复，挂起结果携带书签请求
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OnResumableActivity_SuspendsWithBookmark()
    {
        var activity = new RecordingResumableActivity();

        var result = await activity.ExecuteAsync(WorkflowTestModels.CreateExecutionContext());

        Assert.Equal(ActivityExecutionResultKind.Suspended, result.Kind);
        Assert.Single(result.Bookmarks);
        Assert.Equal(WorkflowBookmarkKinds.Signal, result.Bookmarks[0].Kind);
    }

    /// <summary>
    /// 可补偿活动的补偿方法可经接口分派调用
    /// </summary>
    [Fact]
    public async Task CompensateAsync_ThroughInterface_IsInvoked()
    {
        var activity = new RecordingCompensableActivity();
        ICompensableWorkflowActivity compensable = activity;

        await compensable.CompensateAsync(WorkflowTestModels.CreateExecutionContext());

        Assert.Equal(1, activity.CompensateCount);
    }

    /// <summary>
    /// 只实现基础接口的最小活动
    /// </summary>
    private sealed class PlainActivity : IWorkflowActivity
    {
        /// <summary>
        /// 执行活动
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <returns>完成结果</returns>
        public Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
        {
            return Task.FromResult(ActivityExecutionResult.Complete(outcome: "plain"));
        }
    }

    /// <summary>
    /// 记录恢复调用的可恢复活动
    /// </summary>
    private sealed class RecordingResumableActivity : IResumableWorkflowActivity
    {
        /// <summary>
        /// 恢复时消费的书签种类
        /// </summary>
        public string? ResumedBookmarkKind { get; private set; }

        /// <summary>
        /// 执行活动（挂起并声明信号书签）
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <returns>挂起结果</returns>
        public Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
        {
            var request = new WorkflowBookmarkRequest { Kind = WorkflowBookmarkKinds.Signal, Key = "paid" };
            return Task.FromResult(ActivityExecutionResult.Suspend(request));
        }

        /// <summary>
        /// 恢复活动（把恢复输入合并为输出）
        /// </summary>
        /// <param name="context">恢复上下文</param>
        /// <returns>完成结果</returns>
        public Task<ActivityExecutionResult> ResumeAsync(ActivityResumeContext context)
        {
            ResumedBookmarkKind = context.Bookmark.Kind;
            return Task.FromResult(ActivityExecutionResult.Complete(new Dictionary<string, object?>(context.Inputs)));
        }
    }

    /// <summary>
    /// 记录补偿调用次数的可补偿活动
    /// </summary>
    private sealed class RecordingCompensableActivity : ICompensableWorkflowActivity
    {
        /// <summary>
        /// 补偿调用次数
        /// </summary>
        public int CompensateCount { get; private set; }

        /// <summary>
        /// 执行活动
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <returns>完成结果</returns>
        public Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
        {
            return Task.FromResult(ActivityExecutionResult.Complete());
        }

        /// <summary>
        /// 补偿活动
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <returns>任务</returns>
        public Task CompensateAsync(ActivityExecutionContext context)
        {
            CompensateCount++;
            return Task.CompletedTask;
        }
    }
}
