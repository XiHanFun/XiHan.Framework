// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Definitions;

/// <summary>
/// 流程变量声明模型测试
/// </summary>
/// <remarks>
/// Required 默认 false、DefaultValue 默认 null 决定了启动校验的宽松口径；
/// DefaultValue 是弱类型 object?，往返后退化成 JsonElement，需经值转换器取回原生值。
/// </remarks>
public class WorkflowVariableDefinitionTests
{
    /// <summary>
    /// 新建变量声明的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreOptionalWithoutDefaultValue()
    {
        var variable = new WorkflowVariableDefinition();

        Assert.Equal(string.Empty, variable.Name);
        Assert.Null(variable.Type);
        Assert.False(variable.Required);
        Assert.Null(variable.DefaultValue);
        Assert.Null(variable.Description);
    }

    /// <summary>
    /// 必填变量声明 JSON 往返保留标量字段
    /// </summary>
    [Fact]
    public void JsonRoundTrip_RequiredVariable_PreservesScalarFields()
    {
        var variable = new WorkflowVariableDefinition
        {
            Name = "days",
            Type = "number",
            Required = true,
            Description = "请假天数"
        };

        var restored = JsonSerializer.Deserialize<WorkflowVariableDefinition>(JsonSerializer.Serialize(variable));

        Assert.NotNull(restored);
        Assert.Equal("days", restored.Name);
        Assert.Equal("number", restored.Type);
        Assert.True(restored.Required);
        Assert.Equal("请假天数", restored.Description);
        Assert.Null(restored.DefaultValue);
    }

    /// <summary>
    /// 默认值往返后退化为 JsonElement，需经值转换器归一化
    /// </summary>
    [Fact]
    public void JsonRoundTrip_DefaultValue_NormalizesBackToPrimitive()
    {
        var variable = new WorkflowVariableDefinition { Name = "days", DefaultValue = 3 };

        var restored = JsonSerializer.Deserialize<WorkflowVariableDefinition>(JsonSerializer.Serialize(variable));

        Assert.NotNull(restored);
        Assert.Equal(3m, WorkflowValueConverter.Normalize(restored.DefaultValue));
        Assert.Equal(3, WorkflowValueConverter.ConvertTo<int>(restored.DefaultValue));
    }

    /// <summary>
    /// 默认值支持复杂对象并可归一化为字典
    /// </summary>
    [Fact]
    public void JsonRoundTrip_ObjectDefaultValue_NormalizesToDictionary()
    {
        var variable = new WorkflowVariableDefinition
        {
            Name = "applicant",
            Type = "object",
            DefaultValue = new Dictionary<string, object?> { ["id"] = "u-1", ["level"] = 2 }
        };

        var restored = JsonSerializer.Deserialize<WorkflowVariableDefinition>(JsonSerializer.Serialize(variable));

        Assert.NotNull(restored);
        var normalized = Assert.IsType<Dictionary<string, object?>>(WorkflowValueConverter.Normalize(restored.DefaultValue));
        Assert.Equal("u-1", normalized["id"]);
        Assert.Equal(2m, normalized["level"]);
    }
}
