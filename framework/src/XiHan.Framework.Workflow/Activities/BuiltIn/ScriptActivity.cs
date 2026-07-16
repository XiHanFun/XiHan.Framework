#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptActivity
// Guid:1b76e0c4-92d5-4a38-bf17-84e60d53c2a9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:14:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Options;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// C# 脚本活动（基于框架 Roslyn 脚本引擎执行动态代码）
/// </summary>
/// <remarks>
/// 节点属性：<c>Code</c>（方法体代码，通过 <c>variables</c> 字典读写实例变量，可 <c>return</c> 结果值）；
/// <c>ResultVariable</c>（return 值写入的变量名）。
/// 脚本对 <c>variables</c> 字典的修改会在执行成功后合并回实例变量。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.Script, DisplayName = "C# 脚本", Category = "集成")]
public class ScriptActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var code = GetProperty<string>(context, "Code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return ActivityExecutionResult.Fault($"脚本节点 {context.Node.Id} 未配置 Code");
        }

        var resultVariable = GetProperty<string>(context, "ResultVariable");

        // 变量以归一化副本传入脚本，执行成功后整体合并回实例变量
        var variables = new Dictionary<string, object?>();
        foreach (var name in context.Variables.Names)
        {
            variables[name] = context.Variables.Get(name);
        }

        var methodCode = $$"""
            public static object Execute(System.Collections.Generic.IDictionary<string, object> variables)
            {
            {{code}}
                return null;
            }
            """;

        var scriptEngine = context.ServiceProvider.GetRequiredService<IScriptEngine>();
        var options = new ScriptOptions()
            .WithScriptType(ScriptType.Method)
            .AddGlobal("variables", variables);

        var result = await scriptEngine.ExecuteAsync(methodCode, options);
        if (!result.IsSuccess)
        {
            return ActivityExecutionResult.Fault($"脚本节点 {context.Node.Id} 执行失败：{result.ErrorMessage}");
        }

        var outputs = new Dictionary<string, object?>(variables);
        if (!string.IsNullOrWhiteSpace(resultVariable))
        {
            outputs[resultVariable] = result.Value;
        }

        return ActivityExecutionResult.Complete(outputs);
    }
}
