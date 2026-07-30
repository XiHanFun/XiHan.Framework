// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 可恢复的工作流活动（挂起后由书签恢复时自定义恢复逻辑）
/// </summary>
/// <remarks>
/// 未实现本接口的活动被书签恢复时按默认语义处理：恢复输入合并为输出并完成节点。
/// </remarks>
public interface IResumableWorkflowActivity : IWorkflowActivity
{
    /// <summary>
    /// 恢复活动
    /// </summary>
    /// <param name="context">恢复上下文</param>
    /// <returns>执行结果（完成继续流转；挂起且无新书签请求时保留既有兄弟书签）</returns>
    Task<ActivityExecutionResult> ResumeAsync(ActivityResumeContext context);
}
