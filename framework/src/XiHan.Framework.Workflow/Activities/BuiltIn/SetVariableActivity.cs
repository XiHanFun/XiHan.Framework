#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SetVariableActivity
// Guid:c95d21e8-04f7-4b3a-a680-5d17f92c48e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:06:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 设置变量活动
/// </summary>
/// <remarks>
/// 节点属性：<c>Expressions</c>（变量名 → 表达式，按表达式求值后写入）；<c>Values</c>（变量名 → 字面量，直接写入）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.SetVariable, DisplayName = "设置变量", Category = "数据")]
public class SetVariableActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var outputs = new Dictionary<string, object?>();

        var literals = GetProperty<Dictionary<string, object?>>(context, "Values");
        if (literals is not null)
        {
            foreach (var pair in literals)
            {
                outputs[pair.Key] = WorkflowValueConverter.Normalize(pair.Value);
            }
        }

        var expressions = GetProperty<Dictionary<string, string>>(context, "Expressions");
        if (expressions is not null)
        {
            var evaluator = GetEvaluator(context);
            foreach (var pair in expressions)
            {
                outputs[pair.Key] = await evaluator.EvaluateAsync(pair.Value, context.Variables.AsReadOnly, context.CancellationToken);
            }
        }

        return ActivityExecutionResult.Complete(outputs);
    }
}
