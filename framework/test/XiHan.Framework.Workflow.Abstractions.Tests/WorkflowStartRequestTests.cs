// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 启动请求的公开面：子流程的父链接只由引擎设置
/// </summary>
public sealed class WorkflowStartRequestTests
{
    [Theory]
    [InlineData(nameof(WorkflowStartRequest.InstanceId))]
    [InlineData(nameof(WorkflowStartRequest.ParentInstanceId))]
    [InlineData(nameof(WorkflowStartRequest.ParentNodeInstanceId))]
    [InlineData(nameof(WorkflowStartRequest.Depth))]
    public void EngineOnlyFields_AreNotSettableByCallers(string name)
    {
        // 公开可写时，同分区的调用方能伪造父链接，子实例结束时恢复别人子流程的书签，负的深度还能绕过嵌套上限
        var property = typeof(WorkflowStartRequest).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.False(property.GetMethod!.IsPublic, $"'{name}' must not be readable outside the engine.");
        Assert.False(property.SetMethod!.IsPublic, $"'{name}' must not be settable outside the engine.");
    }
}
