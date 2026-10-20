// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Assignees;

/// <summary>
/// 受理人规则与内置规则类型测试
/// </summary>
/// <remarks>
/// 规则作为节点属性随流程定义 JSON 落库，设计器按 <c>{ type, values }</c> 写入；
/// 内置类型名会出现在存量定义里，必须锁死。
/// </remarks>
public class WorkflowAssigneeRuleTests
{
    /// <summary>
    /// 新建规则的默认值：类型为空串、参数为空列表（不是 null）
    /// </summary>
    [Fact]
    public void Defaults_OnNewRule_AreEmpty()
    {
        var rule = new WorkflowAssigneeRule();

        Assert.Equal(string.Empty, rule.Type);
        Assert.Empty(rule.Values);
    }

    /// <summary>
    /// 设计器写入的驼峰 JSON 能经值转换器还原成规则列表
    /// </summary>
    [Fact]
    public void ConvertTo_FromCamelCaseJson_RestoresRules()
    {
        using var document = JsonDocument.Parse("""[{ "type": "Role", "values": ["10", "11"] }, { "type": "Starter", "values": [] }]""");

        var rules = WorkflowValueConverter.ConvertTo<List<WorkflowAssigneeRule>>(document.RootElement.Clone());

        Assert.NotNull(rules);
        Assert.Equal(2, rules.Count);
        Assert.Equal("Role", rules[0].Type);
        Assert.Equal(["10", "11"], rules[0].Values);
        Assert.Equal("Starter", rules[1].Type);
        Assert.Empty(rules[1].Values);
    }

    /// <summary>
    /// 内置规则类型字面值锁定
    /// </summary>
    [Fact]
    public void BuiltInRuleTypes_AreLocked()
    {
        Assert.Equal("User", WorkflowAssigneeRuleTypes.User);
        Assert.Equal("Starter", WorkflowAssigneeRuleTypes.Starter);
        Assert.Equal("Expression", WorkflowAssigneeRuleTypes.Expression);
    }
}
