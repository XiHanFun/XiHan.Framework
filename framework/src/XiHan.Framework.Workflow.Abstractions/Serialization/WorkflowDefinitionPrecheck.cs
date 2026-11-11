// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Serialization;

/// <summary>
/// 构造定义模型之前对定义 JSON 的核对
/// </summary>
/// <remarks>
/// 模型的 <c>init</c> 与序列化器拒绝的值（缺少必填成员、名字与活动类型的文法、定义 ID、版本、分类长度、超时、节点与转移 ID、变量名、重试策略）在这里按同一组规则先查一遍：值的规则是 <see cref="WorkflowDefinitionRules"/>、<see cref="WorkflowName.IsValid"/>、<see cref="ActivityType.IsValid"/>、<see cref="WorkflowIdentifiers.IsValidElementId"/>，与模型共用；必填成员取自序列化器的元数据。每条问题是一条带路径、消息键与参数的 <see cref="SchemaViolation"/>（ADR-0106 修订）。值的类型不对时不在这里报：交给随后的反序列化报 <see cref="WorkflowMessages.DefinitionJsonMalformed"/>
/// </remarks>
internal static class WorkflowDefinitionPrecheck
{
    /// <summary>
    /// 核对整份定义
    /// </summary>
    /// <remarks>
    /// 根不是对象时没有问题可报（反序列化会报类型不对）
    /// </remarks>
    public static IReadOnlyList<SchemaViolation> Check(JsonElement root)
    {
        var issues = new List<SchemaViolation>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            return issues;
        }

        var definition = WorkflowJsonContext.Default.WorkflowDefinition;
        Required(root, "$", definition, issues);
        Rule(root, "$", nameof(WorkflowDefinition.Id), WorkflowJsonContext.Default.String, WorkflowDefinitionRules.IsValidDefinitionId, static _ => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionIdEmpty), issues);
        Rule(root, "$", nameof(WorkflowDefinition.Name), WorkflowJsonContext.Default.String, WorkflowName.IsValid, static name => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionNameInvalid, MessageArguments.Of(("value", name ?? string.Empty), ("max", WorkflowName.MaxLength))), issues);
        Rule(root, "$", nameof(WorkflowDefinition.Version), WorkflowJsonContext.Default.Int32, WorkflowDefinitionRules.IsValidVersion, static version => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionVersionInvalid, MessageArguments.Of(("value", version))), issues);
        Rule(root, "$", nameof(WorkflowDefinition.Category), WorkflowJsonContext.Default.String, WorkflowDefinitionRules.IsValidCategory, static category => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionCategoryTooLong, MessageArguments.Of(("length", category?.Length ?? 0), ("max", WorkflowDefinition.MaxCategoryLength))), issues);
        Rule(root, "$", nameof(WorkflowDefinition.Timeout), WorkflowJsonContext.Default.NullableTimeSpan, WorkflowDefinitionRules.IsValidTimeout, Timeout, issues);

        foreach (var (node, path) in Items(root, nameof(WorkflowDefinition.Nodes)))
        {
            var type = WorkflowJsonContext.Default.WorkflowNode;
            Required(node, path, type, issues);
            Rule(node, path, nameof(WorkflowNode.Id), WorkflowJsonContext.Default.String, WorkflowIdentifiers.IsValidElementId, static id => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionNodeIdInvalid, MessageArguments.Of(("value", id ?? string.Empty), ("max", WorkflowIdentifiers.MaxElementIdLength))), issues);
            Rule(node, path, nameof(WorkflowNode.ActivityType), WorkflowJsonContext.Default.String, ActivityType.IsValid, static activity => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionActivityTypeInvalid, MessageArguments.Of(("value", activity ?? string.Empty), ("max", ActivityType.MaxLength))), issues);
            Rule(node, path, nameof(WorkflowNode.Timeout), WorkflowJsonContext.Default.NullableTimeSpan, WorkflowDefinitionRules.IsValidTimeout, Timeout, issues);
            if (Member(node, path, nameof(WorkflowNode.RetryPolicy), out var retry, out var retryPath) && retry.ValueKind == JsonValueKind.Object)
            {
                Rule(retry, retryPath, nameof(WorkflowRetryPolicy.MaxAttempts), WorkflowJsonContext.Default.Int32, WorkflowDefinitionRules.IsValidMaxAttempts, static attempts => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionRetryAttemptsInvalid, MessageArguments.Of(("value", attempts))), issues);
                Rule(retry, retryPath, nameof(WorkflowRetryPolicy.FirstDelay), WorkflowJsonContext.Default.TimeSpan, WorkflowDefinitionRules.IsValidFirstDelay, static delay => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionRetryDelayNegative, MessageArguments.Of(("value", delay.ToString("c", CultureInfo.InvariantCulture)))), issues);
                Rule(retry, retryPath, nameof(WorkflowRetryPolicy.BackoffFactor), WorkflowJsonContext.Default.Double, WorkflowDefinitionRules.IsValidBackoffFactor, static backoff => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionRetryBackoffInvalid, MessageArguments.Of(("value", backoff))), issues);
            }
        }

        foreach (var (transition, path) in Items(root, nameof(WorkflowDefinition.Transitions)))
        {
            var type = WorkflowJsonContext.Default.WorkflowTransition;
            Required(transition, path, type, issues);
            Rule(transition, path, nameof(WorkflowTransition.Id), WorkflowJsonContext.Default.String, WorkflowIdentifiers.IsValidElementId, static id => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionTransitionIdInvalid, MessageArguments.Of(("value", id ?? string.Empty), ("max", WorkflowIdentifiers.MaxElementIdLength))), issues);
        }

        foreach (var (variable, path) in Items(root, nameof(WorkflowDefinition.Variables)))
        {
            var type = WorkflowJsonContext.Default.WorkflowVariableDefinition;
            Required(variable, path, type, issues);
            Rule(variable, path, nameof(WorkflowVariableDefinition.Name), WorkflowJsonContext.Default.String, WorkflowDefinitionRules.IsValidVariableName, static _ => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionVariableNameEmpty), issues);
        }

        return issues;
    }

    /// <summary>
    /// 超时不是正数的错误（定义与节点相同）
    /// </summary>
    private static Error Timeout(TimeSpan? timeout)
        => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionTimeoutInvalid, MessageArguments.Of(("value", (timeout ?? TimeSpan.Zero).ToString("c", CultureInfo.InvariantCulture))));

    /// <summary>
    /// 按一条值的规则核对一个成员
    /// </summary>
    /// <remarks>
    /// 在场的成员用序列化器同一个转换器读出，过不了规则就报 <paramref name="invalid"/>；类型不对时不报（反序列化会报）。不在场的成员不报：必填成员缺了由 <see cref="Required"/> 报，非必填成员取模型的初值（ADR-0107），初值本身过得了规则
    /// </remarks>
    private static void Rule<T>(JsonElement owner, string ownerPath, string clrName, JsonTypeInfo<T> type, Func<T?, bool> valid, Func<T?, Error> invalid, List<SchemaViolation> issues)
    {
        if (!Member(owner, ownerPath, clrName, out var element, out var path))
        {
            return;
        }

        T? value;
        try
        {
            value = element.Deserialize(type);
        }
        catch (JsonException)
        {
            // 类型不对不是这里的规则：反序列化会以 json_malformed 报出同一处，这里不重复
            return;
        }

        if (!valid(value))
        {
            Add(issues, path, invalid(value));
        }
    }

    /// <summary>
    /// 必填成员（序列化器元数据里的 <see cref="JsonPropertyInfo.IsRequired"/>，即模型的 <c>required</c>）缺了就报
    /// </summary>
    private static void Required(JsonElement owner, string path, JsonTypeInfo type, List<SchemaViolation> issues)
    {
        foreach (var property in type.Properties)
        {
            if (property.IsRequired && !owner.TryGetProperty(property.Name, out _))
            {
                Add(issues, path + "." + property.Name, MemberMissing(property.Name));
            }
        }
    }

    /// <summary>
    /// 缺少成员的错误
    /// </summary>
    private static Error MemberMissing(string member)
        => Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionMemberMissing, MessageArguments.Of(("member", member)));

    /// <summary>
    /// 根对象上数组成员里的对象项与它们的路径
    /// </summary>
    /// <remarks>
    /// 成员不是数组、项不是对象时跳过（反序列化会报类型不对）
    /// </remarks>
    private static IEnumerable<(JsonElement Item, string Path)> Items(JsonElement root, string clrName)
    {
        if (!Member(root, "$", clrName, out var array, out var path) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                yield return (item, $"{path}[{index}]");
            }

            index++;
        }
    }

    /// <summary>
    /// 按模型属性名（序列化成 camelCase，与序列化器同一命名策略）取成员
    /// </summary>
    private static bool Member(JsonElement owner, string ownerPath, string clrName, out JsonElement value, out string path)
    {
        var name = JsonNamingPolicy.CamelCase.ConvertName(clrName);
        path = ownerPath + "." + name;
        return owner.TryGetProperty(name, out value);
    }

    /// <summary>
    /// 记一条问题：错误的消息键与参数原样带上，原因文本是错误按进程默认区域渲染的消息
    /// </summary>
    private static void Add(List<SchemaViolation> issues, string path, Error error)
        => issues.Add(new SchemaViolation(path, error.Message) { MessageKey = error.MessageKey, Arguments = error.Details });
}
