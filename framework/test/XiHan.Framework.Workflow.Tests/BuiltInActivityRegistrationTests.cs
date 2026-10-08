// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Activities;
using XiHan.Framework.Workflow.Activities.BuiltIn;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 验证高风险内置活动的默认注册边界。
/// </summary>
public sealed class BuiltInActivityRegistrationTests
{
    /// <summary>
    /// 默认工作流服务不注册可执行任意 C# 的脚本活动。
    /// </summary>
    [Fact]
    public void AddXiHanWorkflow_DoesNotRegisterScriptActivityByDefault()
    {
        using var host = new WorkflowTestHost();
        var registry = host.Provider.GetRequiredService<IWorkflowActivityRegistry>();

        Assert.False(registry.TryGet(WorkflowActivityTypes.Script, out _));
    }

    /// <summary>
    /// 受信任宿主可以显式注册脚本活动。
    /// </summary>
    [Fact]
    public void AddXiHanWorkflowActivity_ExplicitlyRegistersScriptActivity()
    {
        using var host = new WorkflowTestHost(services =>
            services.AddXiHanWorkflowActivity<ScriptActivity>());
        var registry = host.Provider.GetRequiredService<IWorkflowActivityRegistry>();

        Assert.True(registry.TryGet(WorkflowActivityTypes.Script, out var descriptor));
        Assert.Equal(typeof(ScriptActivity), descriptor.ClrType);
    }
}
