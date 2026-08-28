// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Activities;

/// <summary>
/// 活动执行上下文测试
/// </summary>
/// <remarks>
/// 上下文的六个必填成员全部是 init-only，构造后不可替换；
/// 关键契约是变量容器与实例变量字典共享同一份引用——活动写变量必须直接反映到实例上。
/// </remarks>
public class ActivityExecutionContextTests
{
    /// <summary>
    /// 必填成员构造后原样可读
    /// </summary>
    [Fact]
    public void Create_WithRequiredMembers_ExposesAllParts()
    {
        var context = WorkflowTestModels.CreateExecutionContext();

        Assert.Equal("def-1", context.Definition.Id);
        Assert.Equal("ins-1", context.Instance.Id);
        Assert.Equal("start", context.Node.Id);
        Assert.Equal("ni-1", context.NodeInstance.Id);
        Assert.NotNull(context.Variables);
        Assert.NotNull(context.ServiceProvider);
    }

    /// <summary>
    /// 未显式赋值时取消令牌为 None
    /// </summary>
    [Fact]
    public void CancellationToken_WhenNotAssigned_IsNone()
    {
        var context = WorkflowTestModels.CreateExecutionContext();

        Assert.Equal(CancellationToken.None, context.CancellationToken);
        Assert.False(context.CancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// 显式赋值的取消令牌原样透出
    /// </summary>
    [Fact]
    public void CancellationToken_WhenAssigned_FlowsThrough()
    {
        using var source = new CancellationTokenSource();

        var context = WorkflowTestModels.CreateExecutionContext(cancellationToken: source.Token);
        source.Cancel();

        Assert.True(context.CancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// 变量容器写入直接反映到实例变量字典
    /// </summary>
    [Fact]
    public void Variables_WhenWritten_UpdatesInstanceVariables()
    {
        var context = WorkflowTestModels.CreateExecutionContext();

        context.Variables.Set("amount", 2000);

        Assert.True(context.Instance.Variables.ContainsKey("amount"));
        Assert.Equal(2000, context.Instance.Variables["amount"]);
    }

    /// <summary>
    /// 传入的实例变量字典被容器直接持有而非复制
    /// </summary>
    [Fact]
    public void Variables_WhenBuiltFromExistingDictionary_SharesSameInstance()
    {
        var variables = new Dictionary<string, object?> { ["name"] = "张三" };

        var context = WorkflowTestModels.CreateExecutionContext(variables);
        variables["name"] = "李四";

        Assert.Equal("李四", context.Variables.Get("name"));
        Assert.Same(variables, context.Instance.Variables);
    }

    /// <summary>
    /// 服务提供者按接口约定解析不到服务时返回 null
    /// </summary>
    [Fact]
    public void ServiceProvider_ForUnknownService_ReturnsNull()
    {
        var context = WorkflowTestModels.CreateExecutionContext();

        Assert.Null(context.ServiceProvider.GetService(typeof(IWorkflowActivity)));
    }
}
