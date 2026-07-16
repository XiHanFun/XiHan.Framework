#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowDefinitionJsonSerializer
// Guid:4d17f0b6-83c2-4a95-be08-d16e59c02a73
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;

namespace XiHan.Framework.Workflow.Builders;

/// <summary>
/// 流程定义 JSON 序列化器（前端设计器与导入导出的稳定格式）
/// </summary>
/// <remarks>
/// 格式约定：camelCase 属性名、枚举字符串化、忽略大小写反序列化；
/// 节点属性字典的值反序列化后为 JsonElement，由 <c>WorkflowValueConverter</c> 在使用处归一化。
/// </remarks>
public static class WorkflowDefinitionJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// 序列化流程定义
    /// </summary>
    /// <param name="definition">流程定义</param>
    /// <returns>JSON 文本</returns>
    public static string Serialize(WorkflowDefinition definition)
    {
        return JsonSerializer.Serialize(definition, Options);
    }

    /// <summary>
    /// 反序列化流程定义
    /// </summary>
    /// <param name="json">JSON 文本</param>
    /// <returns>流程定义</returns>
    /// <exception cref="WorkflowException">JSON 非法或不是流程定义时抛出</exception>
    public static WorkflowDefinition Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkflowDefinition>(json, Options)
                ?? throw new WorkflowException("流程定义 JSON 反序列化结果为空");
        }
        catch (JsonException ex)
        {
            throw new WorkflowException($"流程定义 JSON 非法：{ex.Message}", ex);
        }
    }
}
