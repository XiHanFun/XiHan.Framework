#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UserTaskTests
// Guid:5c94e2f8-07d1-4b63-a8e5-19f60c34d7b2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 12:06:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 人工任务测试（或签/会签/依次/转办/加签/超时）
/// </summary>
public class UserTaskTests : IDisposable
{
    private readonly WorkflowTestHost _host = new();

    private async Task<WorkflowDefinition> PublishApprovalAsync(string code, string policy, params string[] assignees)
    {
        var definition = WorkflowDefinitionBuilder.Create(code, "审批流程")
            .AddStart()
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", assignees.ToList())
                .WithProperty("CompletionPolicy", policy)
                .WithProperty("Title", "单据 {{billNo}} 审批"))
            .AddNode("accepted", WorkflowActivityTypes.SetVariable, "通过", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["result"] = "accepted" }))
            .AddNode("denied", WorkflowActivityTypes.SetVariable, "拒绝", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["result"] = "denied" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "accepted", "outcome == 'approved'")
            .AddTransition("approve", "denied", "outcome == 'rejected'")
            .AddTransition("accepted", "end")
            .AddTransition("denied", "end")
            .Build();
        return await _host.PublishAsync(definition);
    }

    private Task<WorkflowInstance> StartAsync(string code)
    {
        return _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = code,
            Variables = new() { ["billNo"] = "B001" }
        });
    }

    /// <summary>
    /// 或签：任一受理人同意即通过，其余待办消失
    /// </summary>
    [Fact]
    public async Task 或签任一同意即通过()
    {
        await PublishApprovalAsync("any-approve", "Any", "u1", "u2");
        var instance = await StartAsync("any-approve");

        var tasksOfU1 = await _host.UserTaskService.GetPendingAsync("u1");
        var tasksOfU2 = await _host.UserTaskService.GetPendingAsync("u2");
        Assert.Single(tasksOfU1);
        Assert.Single(tasksOfU2);
        Assert.Equal("单据 B001 审批", tasksOfU1[0].Title);

        var result = await _host.UserTaskService.CompleteAsync(tasksOfU1[0].TaskId, "u1", WorkflowUserTaskOutcomes.Approved, "同意");

        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
        Assert.Equal("accepted", new WorkflowVariables(result.Variables).Get<string>("result"));
        Assert.Empty(await _host.UserTaskService.GetPendingAsync("u2"));
    }

    /// <summary>
    /// 会签：全部同意才通过
    /// </summary>
    [Fact]
    public async Task 会签全部同意才通过()
    {
        await PublishApprovalAsync("all-approve", "All", "u1", "u2");
        var instance = await StartAsync("all-approve");

        var firstTask = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        var afterFirst = await _host.UserTaskService.CompleteAsync(firstTask.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Running, afterFirst.Status);

        var secondTask = (await _host.UserTaskService.GetPendingAsync("u2")).Single();
        var afterSecond = await _host.UserTaskService.CompleteAsync(secondTask.TaskId, "u2", WorkflowUserTaskOutcomes.Approved);

        Assert.Equal(WorkflowInstanceStatus.Completed, afterSecond.Status);
        Assert.Equal("accepted", new WorkflowVariables(afterSecond.Variables).Get<string>("result"));
    }

    /// <summary>
    /// 会签：任一拒绝一票否决
    /// </summary>
    [Fact]
    public async Task 会签任一拒绝一票否决()
    {
        await PublishApprovalAsync("all-reject", "All", "u1", "u2");
        await StartAsync("all-reject");

        var task = (await _host.UserTaskService.GetPendingAsync("u2")).Single();
        var result = await _host.UserTaskService.CompleteAsync(task.TaskId, "u2", WorkflowUserTaskOutcomes.Rejected, "不同意");

        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
        Assert.Equal("denied", new WorkflowVariables(result.Variables).Get<string>("result"));
        Assert.Empty(await _host.UserTaskService.GetPendingAsync("u1"));
    }

    /// <summary>
    /// 依次审批：按顺序逐一生成待办
    /// </summary>
    [Fact]
    public async Task 依次审批按顺序推进()
    {
        await PublishApprovalAsync("seq-approve", "Sequential", "u1", "u2");
        await StartAsync("seq-approve");

        // 仅第一位有待办
        Assert.Single(await _host.UserTaskService.GetPendingAsync("u1"));
        Assert.Empty(await _host.UserTaskService.GetPendingAsync("u2"));

        var first = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        var afterFirst = await _host.UserTaskService.CompleteAsync(first.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Running, afterFirst.Status);

        var second = (await _host.UserTaskService.GetPendingAsync("u2")).Single();
        var afterSecond = await _host.UserTaskService.CompleteAsync(second.TaskId, "u2", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Completed, afterSecond.Status);
        Assert.Equal("accepted", new WorkflowVariables(afterSecond.Variables).Get<string>("result"));
    }

    /// <summary>
    /// 转办：任务移交后新受理人可办理，原受理人不可
    /// </summary>
    [Fact]
    public async Task 转办后新受理人办理()
    {
        await PublishApprovalAsync("transfer", "Any", "u1");
        await StartAsync("transfer");

        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        await _host.UserTaskService.TransferAsync(task.TaskId, "u1", "u9", "请代审");

        Assert.Empty(await _host.UserTaskService.GetPendingAsync("u1"));
        var transferred = (await _host.UserTaskService.GetPendingAsync("u9")).Single();

        await Assert.ThrowsAsync<WorkflowException>(() =>
            _host.UserTaskService.CompleteAsync(transferred.TaskId, "u1", WorkflowUserTaskOutcomes.Approved));

        var result = await _host.UserTaskService.CompleteAsync(transferred.TaskId, "u9", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
    }

    /// <summary>
    /// 加签：会签追加受理人后须全部同意
    /// </summary>
    [Fact]
    public async Task 会签加签后新受理人参与()
    {
        await PublishApprovalAsync("add-sign", "All", "u1");
        await StartAsync("add-sign");

        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        await _host.UserTaskService.AddAssigneesAsync(task.TaskId, "u1", ["u2"], "请一起审");

        var afterFirst = await _host.UserTaskService.CompleteAsync(task.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Running, afterFirst.Status);

        var addedTask = (await _host.UserTaskService.GetPendingAsync("u2")).Single();
        var result = await _host.UserTaskService.CompleteAsync(addedTask.TaskId, "u2", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
        Assert.Equal("accepted", new WorkflowVariables(result.Variables).Get<string>("result"));
    }

    /// <summary>
    /// 节点超时：走 timeout 结果边
    /// </summary>
    [Fact]
    public async Task 审批超时走超时分支()
    {
        var definition = WorkflowDefinitionBuilder.Create("timeout-approve", "超时审批")
            .AddStart()
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", new List<string> { "u1" })
                .WithTimeout(3600))
            .AddNode("timeoutPath", WorkflowActivityTypes.SetVariable, "超时", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["result"] = "timeout" }))
            .AddEnd()
            .AddTransition("approve", "end", "outcome == 'approved'")
            .AddTransition("approve", "timeoutPath", "outcome == 'timeout'")
            .AddTransition("timeoutPath", "end")
            .AddTransition("start", "approve")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "timeout-approve" });

        var bookmarks = await _host.BookmarkStore.GetByInstanceAsync(instance.Id);
        Assert.Equal(2, bookmarks.Count);
        var timeoutBookmark = bookmarks.Single(item => item.Kind == WorkflowBookmarkKinds.NodeTimeout);
        Assert.Equal(_host.Clock.Now.AddSeconds(3600), timeoutBookmark.DueTime);

        var result = await _host.Engine.ResumeBookmarkAsync(timeoutBookmark.Id);

        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
        Assert.Equal("timeout", new WorkflowVariables(result.Variables).Get<string>("result"));
        Assert.Empty(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }
}
