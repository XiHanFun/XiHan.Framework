// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 交给活动、结果处理器与指派规则的上下文：实例与节点实例只读
/// </summary>
public sealed class ActivityContextTests
{
    [Theory]
    [InlineData(typeof(ActivityContext), "Instance")]
    [InlineData(typeof(ActivityContext), "NodeInstance")]
    [InlineData(typeof(ActivityResumeContext), "Instance")]
    [InlineData(typeof(ActivityResumeContext), "NodeInstance")]
    [InlineData(typeof(WorkflowOutcomeContext), "Instance")]
    [InlineData(typeof(AssigneeResolution), "Instance")]
    public void ExtensionContexts_ExposeInstancesWithoutSetters(Type contextType, string propertyName)
    {
        // 引擎在活动返回后写回实例与节点实例；交出可写的对象，扩展代码改了状态、租户或父链接会被一起持久化
        var property = contextType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);

        var writable = property.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static member => member.SetMethod?.IsPublic == true)
            .Select(static member => member.Name)
            .ToList();
        Assert.True(writable.Count == 0, $"'{contextType.Name}.{propertyName}' is '{property.PropertyType.Name}' with public setters: {string.Join(", ", writable)}.");
    }
}
