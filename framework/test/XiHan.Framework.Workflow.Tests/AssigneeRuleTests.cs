// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 受理人规则测试（规则解析、自定义解析器、剔除发起人、受理人为空策略、抄送规则）
/// </summary>
public class AssigneeRuleTests : IDisposable
{
    private readonly WorkflowTestHost _host = new(services =>
    {
        services.AddSingleton<RoleDirectory>();
        services.AddXiHanWorkflowAssigneeProvider<TestRoleAssigneeProvider>();
    });

    private RoleDirectory Roles => _host.Provider.GetRequiredService<RoleDirectory>();

    /// <summary>
    /// 构建单人工任务审批流（同意/驳回分别写入 result 变量）
    /// </summary>
    private async Task PublishAsync(string code, Action<WorkflowNodeBuilder> configure)
    {
        var definition = WorkflowDefinitionBuilder.Create(code, "规则审批")
            .AddStart()
            .AddUserTask("approve", "审批", configure)
            .AddNode("accepted", WorkflowActivityTypes.SetVariable, "通过", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["result"] = "accepted" }))
            .AddNode("denied", WorkflowActivityTypes.SetVariable, "驳回", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["result"] = "denied" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "accepted", "outcome == 'approved'")
            .AddTransition("approve", "denied", "outcome == 'rejected'")
            .AddTransition("accepted", "end")
            .AddTransition("denied", "end")
            .Build();
        await _host.PublishAsync(definition);
    }

    private Task<WorkflowInstance> StartAsync(string code, string? starterId = "starter", Dictionary<string, object?>? variables = null)
    {
        return _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = code,
            StarterId = starterId,
            Variables = variables ?? []
        });
    }

    /// <summary>
    /// 三类来源按顺序合并去重：静态列表 → 表达式 → 规则
    /// </summary>
    [Fact]
    public async Task 三类受理人来源按顺序合并去重()
    {
        Roles.Members["finance"] = ["u3", "u1"];
        await PublishAsync("merge", node => node
            .WithProperty("Assignees", new List<string> { "u1" })
            .WithProperty("AssigneesExpression", "approvers")
            .WithAssigneeRule("Role", "finance")
            .WithProperty("CompletionPolicy", "Sequential"));

        var instance = await StartAsync("merge", variables: new() { ["approvers"] = "u2,u1" });

        var nodeInstance = (await _host.InstanceStore.GetNodeInstancesAsync(instance.Id)).Single(item => item.NodeId == "approve");
        var assignees = WorkflowValueConverter.ConvertTo<List<string>>(nodeInstance.State["assignees"]);
        Assert.Equal(["u1", "u2", "u3"], assignees);
    }

    /// <summary>
    /// 自定义规则类型交给注册的解析器，命中的每个人都生成待办
    /// </summary>
    [Fact]
    public async Task 自定义规则类型由注册的解析器解析()
    {
        Roles.Members["manager"] = ["m1", "m2"];
        await PublishAsync("custom-rule", node => node.WithAssigneeRule("Role", "manager"));

        await StartAsync("custom-rule");

        Assert.Single(await _host.UserTaskService.GetPendingAsync("m1"));
        Assert.Single(await _host.UserTaskService.GetPendingAsync("m2"));
    }

    /// <summary>
    /// 剔除发起人：发起人同时命中规则时不给自己生成待办
    /// </summary>
    [Fact]
    public async Task 剔除发起人后不给自己生成待办()
    {
        Roles.Members["admin"] = ["starter", "a1"];
        await PublishAsync("exclude-starter", node => node
            .WithAssigneeRule("Role", "admin")
            .WithProperty("ExcludeStarter", true));

        await StartAsync("exclude-starter", starterId: "starter");

        Assert.Empty(await _host.UserTaskService.GetPendingAsync("starter"));
        Assert.Single(await _host.UserTaskService.GetPendingAsync("a1"));
    }

    /// <summary>
    /// 发起人规则：退回发起人修改节点直接派给发起人
    /// </summary>
    [Fact]
    public async Task 发起人规则派给发起人()
    {
        await PublishAsync("starter-rule", node => node.WithAssigneeRule(WorkflowAssigneeRuleTypes.Starter));

        await StartAsync("starter-rule", starterId: "alice");

        Assert.Single(await _host.UserTaskService.GetPendingAsync("alice"));
    }

    /// <summary>
    /// 受理人为空默认故障，修正组织数据后重试即生成待办
    /// </summary>
    [Fact]
    public async Task 受理人为空默认故障且可重试()
    {
        await PublishAsync("empty-fault", node => node.WithAssigneeRule("Role", "nobody"));

        var instance = await StartAsync("empty-fault");
        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Contains("未解析到受理人", instance.FaultMessage);

        Roles.Members["nobody"] = ["late"];
        var retried = await _host.Engine.RetryAsync(instance.Id);

        Assert.Equal(WorkflowInstanceStatus.Running, retried.Status);
        Assert.Single(await _host.UserTaskService.GetPendingAsync("late"));
    }

    /// <summary>
    /// 受理人为空自动通过：按 approved 流转并在轨迹留下无办理人记录
    /// </summary>
    [Fact]
    public async Task 受理人为空自动通过()
    {
        await PublishAsync("empty-approve", node => node
            .WithAssigneeRule("Role", "nobody")
            .WithProperty("EmptyAssigneeStrategy", "AutoApprove"));

        var instance = await StartAsync("empty-approve");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal("accepted", new WorkflowVariables(instance.Variables).Get<string>("result"));

        var nodeInstance = (await _host.InstanceStore.GetNodeInstancesAsync(instance.Id)).Single(item => item.NodeId == "approve");
        var history = WorkflowValueConverter.ConvertTo<List<Dictionary<string, object?>>>(nodeInstance.State["history"]);
        var entry = Assert.Single(history!);
        Assert.Null(entry["actorId"]);
        Assert.Equal(WorkflowUserTaskOutcomes.Approved, entry["outcome"]);
    }

    /// <summary>
    /// 受理人为空自动驳回：按 rejected 流转
    /// </summary>
    [Fact]
    public async Task 受理人为空自动驳回()
    {
        await PublishAsync("empty-reject", node => node
            .WithAssigneeRule("Role", "nobody")
            .WithProperty("EmptyAssigneeStrategy", "AutoReject"));

        var instance = await StartAsync("empty-reject");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal("denied", new WorkflowVariables(instance.Variables).Get<string>("result"));
    }

    /// <summary>
    /// 受理人为空转兜底：兜底规则同样剔除发起人
    /// </summary>
    [Fact]
    public async Task 受理人为空转交兜底受理人()
    {
        Roles.Members["admin"] = ["starter", "root"];
        await PublishAsync("empty-fallback", node => node
            .WithAssigneeRule("Role", "nobody")
            .WithProperty("ExcludeStarter", true)
            .WithProperty("EmptyAssigneeStrategy", "Fallback")
            .WithProperty("FallbackAssigneeRules", new List<WorkflowAssigneeRule>
            {
                new() { Type = "Role", Values = ["admin"] }
            }));

        await StartAsync("empty-fallback", starterId: "starter");

        Assert.Single(await _host.UserTaskService.GetPendingAsync("root"));
        Assert.Empty(await _host.UserTaskService.GetPendingAsync("starter"));
    }

    /// <summary>
    /// 抄送规则与静态抄送合并后随待办创建事件发出
    /// </summary>
    [Fact]
    public async Task 抄送规则随待办创建事件发出()
    {
        Roles.Members["hr"] = ["h1", "c1"];
        await PublishAsync("cc-rule", node => node
            .WithProperty("Assignees", new List<string> { "u1" })
            .WithProperty("CcUserIds", new List<string> { "c1" })
            .WithProperty("CcRules", new List<WorkflowAssigneeRule> { new() { Type = "Role", Values = ["hr"] } }));

        await StartAsync("cc-rule");

        var created = Assert.Single(_host.Events.OfType<WorkflowUserTaskCreatedEventData>());
        Assert.Equal(["c1", "h1"], created.CcUserIds);
    }

    /// <summary>
    /// 解析器抛出异常时节点故障（fail-closed，不当作无人可审）
    /// </summary>
    [Fact]
    public async Task 解析器异常时节点故障()
    {
        Roles.Unavailable = true;
        await PublishAsync("provider-down", node => node
            .WithAssigneeRule("Role", "manager")
            .WithProperty("EmptyAssigneeStrategy", "AutoApprove"));

        var instance = await StartAsync("provider-down");

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Contains("组织数据不可用", instance.FaultMessage);
    }

    /// <summary>
    /// 发布校验：未注册规则类型、非法参数、Fallback 缺兜底规则、未知空受理人策略
    /// </summary>
    [Fact]
    public async Task 发布拦截非法受理人配置()
    {
        var definition = WorkflowDefinitionBuilder.Create("invalid-rules", "非法规则")
            .AddStart()
            .AddUserTask("unknown-type", "未知类型", node => node.WithAssigneeRule("Ghost", "x"))
            .AddUserTask("bad-expression", "非法表达式", node => node.WithAssigneeRule(WorkflowAssigneeRuleTypes.Expression, "a >"))
            .AddUserTask("missing-fallback", "缺兜底", node => node
                .WithAssigneeRule(WorkflowAssigneeRuleTypes.User, "u1")
                .WithProperty("EmptyAssigneeStrategy", "Fallback"))
            .AddUserTask("bad-strategy", "未知策略", node => node
                .WithAssigneeRule(WorkflowAssigneeRuleTypes.User, "u1")
                .WithProperty("EmptyAssigneeStrategy", "Skip"))
            .AddUserTask("no-source", "无来源", _ => { })
            .AddEnd()
            .AddTransition("start", "unknown-type")
            .AddTransition("unknown-type", "bad-expression")
            .AddTransition("bad-expression", "missing-fallback")
            .AddTransition("missing-fallback", "bad-strategy")
            .AddTransition("bad-strategy", "no-source")
            .AddTransition("no-source", "end")
            .Build();
        var created = await _host.DefinitionManager.CreateAsync(definition);

        var exception = await Assert.ThrowsAsync<WorkflowDefinitionValidationException>(() =>
            _host.DefinitionManager.PublishAsync(created.Id));

        Assert.Contains(exception.Errors, error => error.Contains("unknown-type") && error.Contains("Ghost"));
        Assert.Contains(exception.Errors, error => error.Contains("bad-expression") && error.Contains("非法"));
        Assert.Contains(exception.Errors, error => error.Contains("missing-fallback") && error.Contains("FallbackAssigneeRules"));
        Assert.Contains(exception.Errors, error => error.Contains("bad-strategy") && error.Contains("Skip"));
        Assert.Contains(exception.Errors, error => error.Contains("no-source") && error.Contains("AssigneeRules"));
    }

    /// <summary>
    /// 规则经 JSON 往返（数据库持久化场景，属性变为 JsonElement）后仍能解析
    /// </summary>
    [Fact]
    public async Task 规则经JSON往返后仍能解析()
    {
        Roles.Members["manager"] = ["m1"];
        var definition = WorkflowDefinitionBuilder.Create("json-rules", "往返规则")
            .AddStart()
            .AddUserTask("approve", "审批", node => node.WithAssigneeRule("Role", "manager"))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();

        var restored = WorkflowDefinitionJsonSerializer.Deserialize(WorkflowDefinitionJsonSerializer.Serialize(definition));
        await _host.PublishAsync(restored);
        await StartAsync("json-rules");

        Assert.Single(await _host.UserTaskService.GetPendingAsync("m1"));
    }

    /// <summary>
    /// 同一规则类型注册多个解析器时解析门面构造即失败
    /// </summary>
    [Fact]
    public void 同类型重复注册解析器即失败()
    {
        using var host = new WorkflowTestHost(services =>
        {
            services.AddSingleton<RoleDirectory>();
            services.AddXiHanWorkflowAssigneeProvider<TestRoleAssigneeProvider>();
            services.AddXiHanWorkflowAssigneeProvider<DuplicateRoleAssigneeProvider>();
        });

        var exception = Assert.Throws<WorkflowException>(() => host.Provider.GetRequiredService<IWorkflowAssigneeResolver>());
        Assert.Contains("Role", exception.Message);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _host.Dispose();
    }
}

/// <summary>
/// 测试用角色目录（模拟应用侧组织数据）
/// </summary>
public sealed class RoleDirectory
{
    /// <summary>
    /// 角色成员
    /// </summary>
    public Dictionary<string, List<string>> Members { get; } = [];

    /// <summary>
    /// 是否模拟数据源不可用
    /// </summary>
    public bool Unavailable { get; set; }
}

/// <summary>
/// 测试用角色规则解析器
/// </summary>
public class TestRoleAssigneeProvider : IWorkflowAssigneeProvider
{
    private readonly RoleDirectory _directory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="directory">角色目录</param>
    public TestRoleAssigneeProvider(RoleDirectory directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// 规则类型
    /// </summary>
    public virtual string RuleType => "Role";

    /// <summary>
    /// 校验规则参数
    /// </summary>
    /// <param name="rule">规则</param>
    /// <returns>错误信息列表</returns>
    public IReadOnlyList<string> Validate(WorkflowAssigneeRule rule)
    {
        return rule.Values.Count == 0 ? ["角色规则至少需要一个角色"] : [];
    }

    /// <summary>
    /// 解析受理人
    /// </summary>
    /// <param name="context">解析上下文</param>
    /// <returns>受理人列表</returns>
    public Task<IReadOnlyList<string>> ResolveAsync(WorkflowAssigneeResolveContext context)
    {
        if (_directory.Unavailable)
        {
            throw new InvalidOperationException("组织数据不可用");
        }

        IReadOnlyList<string> members = [.. context.Rule.Values.SelectMany(role => _directory.Members.GetValueOrDefault(role) ?? [])];
        return Task.FromResult(members);
    }
}

/// <summary>
/// 测试用重复类型解析器
/// </summary>
public sealed class DuplicateRoleAssigneeProvider : TestRoleAssigneeProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="directory">角色目录</param>
    public DuplicateRoleAssigneeProvider(RoleDirectory directory)
        : base(directory)
    {
    }

    /// <summary>
    /// 规则类型（与基类同名，大小写不同）
    /// </summary>
    public override string RuleType => "role";
}
