// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Options;

namespace XiHan.Framework.Workflow.Activities;

/// <summary>
/// 活动注册表默认实现（从工作流选项的活动类型列表构建映射）
/// </summary>
public class WorkflowActivityRegistry : IWorkflowActivityRegistry
{
    private readonly Lazy<Dictionary<string, WorkflowActivityDescriptor>> _descriptors;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">工作流选项</param>
    public WorkflowActivityRegistry(IOptions<XiHanWorkflowOptions> options)
    {
        _descriptors = new Lazy<Dictionary<string, WorkflowActivityDescriptor>>(() => Build(options.Value));
    }

    /// <inheritdoc />
    public WorkflowActivityDescriptor Get(string activityType)
    {
        return TryGet(activityType, out var descriptor)
            ? descriptor
            : throw new WorkflowException($"未注册的活动类型 {activityType}");
    }

    /// <inheritdoc />
    public bool TryGet(string activityType, out WorkflowActivityDescriptor descriptor)
    {
        return _descriptors.Value.TryGetValue(activityType, out descriptor!);
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkflowActivityDescriptor> GetAll()
    {
        return [.. _descriptors.Value.Values];
    }

    private static Dictionary<string, WorkflowActivityDescriptor> Build(XiHanWorkflowOptions options)
    {
        var descriptors = new Dictionary<string, WorkflowActivityDescriptor>(StringComparer.Ordinal);

        foreach (var clrType in options.Activities)
        {
            var attribute = clrType.GetCustomAttribute<WorkflowActivityAttribute>();
            var activityType = attribute?.ActivityType
                ?? (clrType.Name.EndsWith("Activity", StringComparison.Ordinal)
                    ? clrType.Name[..^"Activity".Length]
                    : clrType.Name);

            if (descriptors.TryGetValue(activityType, out var existing) && existing.ClrType != clrType)
            {
                throw new WorkflowException($"活动类型编码 {activityType} 被 {existing.ClrType.Name} 与 {clrType.Name} 重复注册");
            }

            descriptors[activityType] = new WorkflowActivityDescriptor
            {
                ActivityType = activityType,
                ClrType = clrType,
                DisplayName = attribute?.DisplayName,
                Category = attribute?.Category,
                OutgoingBehavior = attribute?.OutgoingBehavior ?? ActivityOutgoingBehavior.AllMatched
            };
        }

        return descriptors;
    }
}
