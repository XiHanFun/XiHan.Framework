#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowDefinitionValidator
// Guid:b25e70c1-49d8-4f36-a0e7-81c34d95f6b2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:21:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.UserTasks;
using XiHan.Framework.Workflow.Activities;
using XiHan.Framework.Workflow.Expressions;

namespace XiHan.Framework.Workflow.Definitions;

/// <summary>
/// 流程定义结构校验器（发布前把关，fail-closed）
/// </summary>
public class WorkflowDefinitionValidator
{
    private readonly IWorkflowActivityRegistry _activityRegistry;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="activityRegistry">活动注册表</param>
    public WorkflowDefinitionValidator(IWorkflowActivityRegistry activityRegistry)
    {
        _activityRegistry = activityRegistry;
    }

    /// <summary>
    /// 校验流程定义
    /// </summary>
    /// <param name="definition">流程定义</param>
    /// <returns>校验错误列表（为空表示通过）</returns>
    public List<string> Validate(WorkflowDefinition definition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Code))
        {
            errors.Add("流程编码不能为空");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add("流程名称不能为空");
        }

        if (definition.Nodes.Count == 0)
        {
            errors.Add("流程至少需要一个节点");
            return errors;
        }

        // 节点标识唯一且非空
        var duplicateNodeIds = definition.Nodes
            .GroupBy(node => node.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        errors.AddRange(duplicateNodeIds.Select(id => $"节点标识 {id} 重复"));

        if (definition.Nodes.Any(node => string.IsNullOrWhiteSpace(node.Id)))
        {
            errors.Add("存在标识为空的节点");
        }

        // 活动类型可解析
        foreach (var node in definition.Nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ActivityType) && !_activityRegistry.TryGet(node.ActivityType, out _))
            {
                errors.Add($"节点 {node.Id} 引用了未注册的活动类型 {node.ActivityType}");
            }

            if (string.IsNullOrWhiteSpace(node.ActivityType))
            {
                errors.Add($"节点 {node.Id} 未配置活动类型");
            }
        }

        // 有且仅有一个开始节点
        var startNodes = definition.Nodes.Where(node => node.ActivityType == WorkflowActivityTypes.Start).ToList();
        if (startNodes.Count != 1)
        {
            errors.Add($"流程必须有且仅有一个开始节点，当前为 {startNodes.Count} 个");
        }

        var nodeIds = definition.Nodes.Select(node => node.Id).ToHashSet();

        // 连线引用完整
        foreach (var transition in definition.Transitions)
        {
            if (!nodeIds.Contains(transition.SourceNodeId))
            {
                errors.Add($"连线 {transition.Id} 的源节点 {transition.SourceNodeId} 不存在");
            }

            if (!nodeIds.Contains(transition.TargetNodeId))
            {
                errors.Add($"连线 {transition.Id} 的目标节点 {transition.TargetNodeId} 不存在");
            }

            // 条件表达式语法
            if (!string.IsNullOrWhiteSpace(transition.Condition))
            {
                try
                {
                    WorkflowExpressionEvaluator.ValidateSyntax(transition.Condition);
                }
                catch (WorkflowException ex)
                {
                    errors.Add($"连线 {transition.Id} 的条件表达式非法：{ex.Message}");
                }
            }
        }

        // 开始节点无入边
        if (startNodes.Count == 1)
        {
            var startId = startNodes[0].Id;
            if (definition.Transitions.Any(transition => transition.TargetNodeId == startId))
            {
                errors.Add("开始节点不允许有入边");
            }
        }

        // 独占网关至多一条默认边
        foreach (var node in definition.Nodes)
        {
            if (!_activityRegistry.TryGet(node.ActivityType, out var descriptor)
                || descriptor.OutgoingBehavior != ActivityOutgoingBehavior.Exclusive)
            {
                continue;
            }

            var defaultCount = definition.Transitions.Count(transition => transition.SourceNodeId == node.Id && transition.IsDefault);
            if (defaultCount > 1)
            {
                errors.Add($"独占网关 {node.Id} 配置了 {defaultCount} 条默认分支，至多允许一条");
            }
        }

        // 关键枚举属性在发布时把关，运行期不允许静默降级
        foreach (var node in definition.Nodes)
        {
            if (node.ActivityType == WorkflowActivityTypes.Join
                && node.Properties.TryGetValue("Mode", out var rawMode) && rawMode is not null)
            {
                var mode = WorkflowValueConverter.ConvertTo<string>(WorkflowValueConverter.Normalize(rawMode));
                if (!string.Equals(mode, "WaitAll", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mode, "All", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mode, "WaitAny", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mode, "Any", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"汇聚网关 {node.Id} 配置了未知模式 {mode}（仅支持 WaitAll/WaitAny）");
                }
            }

            if (node.ActivityType == WorkflowActivityTypes.UserTask)
            {
                if (node.Properties.TryGetValue("CompletionPolicy", out var rawPolicy) && rawPolicy is not null)
                {
                    var policy = WorkflowValueConverter.ConvertTo<string>(WorkflowValueConverter.Normalize(rawPolicy));
                    if (!Enum.TryParse<UserTaskCompletionPolicy>(policy, ignoreCase: true, out var parsed)
                        || !Enum.IsDefined(parsed))
                    {
                        errors.Add($"人工任务 {node.Id} 配置了未知完成策略 {policy}（仅支持 Any/All/Sequential）");
                    }
                }

                if (!node.Properties.ContainsKey("Assignees") && !node.Properties.ContainsKey("AssigneesExpression"))
                {
                    errors.Add($"人工任务 {node.Id} 未配置 Assignees 或 AssigneesExpression");
                }
            }
        }

        // 自开始节点的可达性
        if (startNodes.Count == 1)
        {
            var reachable = new HashSet<string>();
            var pending = new Queue<string>();
            pending.Enqueue(startNodes[0].Id);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!reachable.Add(current))
                {
                    continue;
                }

                foreach (var transition in definition.Transitions.Where(item => item.SourceNodeId == current))
                {
                    pending.Enqueue(transition.TargetNodeId);
                }
            }

            errors.AddRange(nodeIds.Except(reachable).Select(id => $"节点 {id} 从开始节点不可达"));
        }

        return errors;
    }

    /// <summary>
    /// 校验流程定义并在失败时抛出校验异常
    /// </summary>
    /// <param name="definition">流程定义</param>
    /// <exception cref="WorkflowDefinitionValidationException">校验失败时抛出</exception>
    public void ValidateAndThrow(WorkflowDefinition definition)
    {
        var errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new WorkflowDefinitionValidationException(errors);
        }
    }
}
