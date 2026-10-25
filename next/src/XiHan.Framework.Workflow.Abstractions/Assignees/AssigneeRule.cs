// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 指派规则：类型（<see cref="AssigneeRuleTypes"/> 或应用登记的类型）与参数。
/// </summary>
/// <param name="Type">规则类型。</param>
/// <param name="Values">参数。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AssigneeRule(string Type, IReadOnlyList<string> Values)
{
    /// <summary>
    /// 规则类型。
    /// </summary>
    public string Type { get; } = !string.IsNullOrWhiteSpace(Type) ? Type : throw new ArgumentException("The rule type must not be empty.", nameof(Type));

    /// <summary>
    /// 参数。
    /// </summary>
    public IReadOnlyList<string> Values { get; } = Values ?? throw new ArgumentNullException(nameof(Values));

    /// <summary>
    /// 从 JSON 数组解析：每项 <c>{ "type": "...", "values": [...] }</c>（<c>values</c> 可是逗号分隔字符串）；<see langword="null"/> 为空列表。
    /// </summary>
    public static Result<IReadOnlyList<AssigneeRule>> Parse(JsonNode? node)
    {
        if (node is null)
        {
            return Result.Success<IReadOnlyList<AssigneeRule>>([]);
        }

        if (node is not JsonArray array)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.AssigneeRuleInvalid, MessageArguments.Of(("type", "?"), ("reason", "rules must be an array")));
        }

        var rules = new List<AssigneeRule>(array.Count);
        foreach (var item in array)
        {
            if (item is not JsonObject obj || !obj.TryGetPropertyValue("type", out var type) || type is not JsonValue typeValue)
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.AssigneeRuleInvalid, MessageArguments.Of(("type", "?"), ("reason", "each rule needs a type")));
            }

            var typeName = WorkflowValues.AsString(typeValue);
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.AssigneeRuleInvalid, MessageArguments.Of(("type", "?"), ("reason", "each rule needs a type")));
            }

            rules.Add(new AssigneeRule(typeName, WorkflowValues.ToStringList(obj.TryGetPropertyValue("values", out var values) ? values : null)));
        }

        return rules;
    }

    /// <summary>
    /// 转成 JSON（设计器与 Builder 用）。
    /// </summary>
    public JsonObject ToJson() => new()
    {
        ["type"] = Type,
        ["values"] = new JsonArray(Values.Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray()),
    };
}
