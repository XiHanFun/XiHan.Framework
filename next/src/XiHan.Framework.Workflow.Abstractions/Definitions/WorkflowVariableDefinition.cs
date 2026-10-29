// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义声明的变量：启动时缺省补默认值，必填且没给值则启动失败。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowVariableDefinition
{
    /// <summary>
    /// 变量名。
    /// </summary>
    public required string Name
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The variable name must not be empty.", nameof(value));
    }

    /// <summary>
    /// 是否必填。
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// 默认值。
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>。
    /// </remarks>
    public JsonNode? DefaultValue { get; init; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; init; }
}
