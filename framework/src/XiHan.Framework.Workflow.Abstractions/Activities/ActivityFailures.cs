// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 内置活动的失败原因：每种一个消息键
/// </summary>
/// <remarks>
/// 面向人的文案按调用方的区域本地化（RFC-0010，ADR-0105），参数里只放属性名与属性的原值，不放英文句子
/// </remarks>
internal static class ActivityFailures
{
    /// <summary>
    /// 节点超时
    /// </summary>
    public static Error TimedOut(WorkflowNode node)
        => Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.NodeTimedOut, details: MessageArguments.Of(("node", node.Id)));

    /// <summary>
    /// 子流程没有正常完成
    /// </summary>
    /// <remarks>
    /// 子实例的故障（回调输入里 <see cref="WorkflowReservedNames.ChildFaultInput"/>，<see cref="WorkflowFault"/> 的 JSON）是内层原因，子实例的文案随外层一起按调用方的区域本地化。故障、取消、终止各一个消息键，状态名不进文案（ADR-0106）；参数 <c>status</c> 仍是枚举名
    /// </remarks>
    public static Error ChildFailed(JsonObject inputs, string status)
    {
        var id = inputs[WorkflowReservedNames.ChildInstanceIdInput] is JsonValue child ? WorkflowValues.AsString(child) : string.Empty;
        var inner = WorkflowJson.DeserializeFault(inputs[WorkflowReservedNames.ChildFaultInput])?.ToError();
        var details = MessageArguments.Of(("id", id), ("status", status));

        // 引擎回调给的是终态的枚举名；手工恢复书签给的别的值没有对应的说法，报通用的键、状态原样
        if (string.Equals(status, nameof(WorkflowInstanceStatus.Faulted), StringComparison.OrdinalIgnoreCase))
        {
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFaulted, details, inner);
        }

        if (string.Equals(status, nameof(WorkflowInstanceStatus.Canceled), StringComparison.OrdinalIgnoreCase))
        {
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildCanceled, details, inner);
        }

        return string.Equals(status, nameof(WorkflowInstanceStatus.Terminated), StringComparison.OrdinalIgnoreCase)
            ? Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildTerminated, details, inner)
            : Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFailed, details, inner);
    }

    /// <summary>
    /// 属性必须是正的秒数
    /// </summary>
    public static Error NotSeconds(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotSeconds, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 属性的秒数超过上限
    /// </summary>
    public static Error SecondsOverLimit(string name, TimeSpan max)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertySecondsOverLimit, MessageArguments.Of(("name", name), ("max", max.TotalSeconds)));

    /// <summary>
    /// 属性必须是日期时间
    /// </summary>
    public static Error NotDateTime(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotDateTime, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 属性不能为空
    /// </summary>
    public static Error Empty(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyEmpty, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 属性的值不是 cron 表达式
    /// </summary>
    public static Error NotCron(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotCron, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// 属性的值不是时区
    /// </summary>
    public static Error NotTimeZone(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotTimeZone, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// cron 表达式五年内没有下一次触发
    /// </summary>
    public static Error CronExhausted(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyCronExhausted, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// 属性至少需要一个信号名
    /// </summary>
    public static Error NoSignal(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNoSignal, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 实例没有关联 ID，又没有允许任意关联
    /// </summary>
    public static Error NeedsCorrelation(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNeedsCorrelation, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 属性的求值结果必须是数组
    /// </summary>
    public static Error NotArray(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotArray, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 属性的值不是绝对 HTTP URL
    /// </summary>
    public static Error NotUrl(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotUrl, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// 属性的值不是支持的 HTTP 方法
    /// </summary>
    public static Error NotHttpMethod(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotHttpMethod, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// 属性里的请求头不合法
    /// </summary>
    public static Error HeaderInvalid(string name, string header)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyHeaderInvalid, MessageArguments.Of(("name", name), ("value", header)));

    /// <summary>
    /// 属性的值不是媒体类型
    /// </summary>
    public static Error NotMediaType(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotMediaType, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// 属性里的项不是表达式
    /// </summary>
    public static Error NotExpression(string name, string entry)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotExpression, MessageArguments.Of(("name", name), ("value", entry)));

    /// <summary>
    /// 属性的值不是工作流名称
    /// </summary>
    public static Error NotWorkflowName(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotWorkflowName, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// 属性的值不在允许的取值里
    /// </summary>
    public static Error UnknownValue(string name, string value)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyUnknownValue, MessageArguments.Of(("name", name), ("value", value)));

    /// <summary>
    /// 属性的值不在允许的取值里（按属性的 JSON 原值）
    /// </summary>
    public static Error UnknownValue(string name, JsonNode? value)
        => UnknownValue(name, value is JsonValue scalar ? WorkflowValues.AsString(scalar) : value?.ToJsonString() ?? string.Empty);

    /// <summary>
    /// 属性必须是正整数或百分比
    /// </summary>
    public static Error NotThreshold(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotThreshold, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 属性必须是整数
    /// </summary>
    public static Error NotInteger(string name)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotInteger, MessageArguments.Of(("name", name)));
}
