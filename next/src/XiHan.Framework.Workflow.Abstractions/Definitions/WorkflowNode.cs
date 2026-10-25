// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义里的一个节点：活动类型加属性；字符串属性支持 <c>{{ 表达式 }}</c> 模板。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowNode
{
    /// <summary>
    /// 节点 ID，定义内唯一。
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = WorkflowIdentifiers.IsValidElementId(value) ? value : throw new ArgumentException($"Node ID '{value}' is invalid.", nameof(value));
    }

    /// <summary>
    /// 活动类型。
    /// </summary>
    public required ActivityType ActivityType { get; init; }

    /// <summary>
    /// 显示名；缺省用 ID。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 活动属性；各活动类型自行解释。
    /// </summary>
    public JsonObject Properties { get; init; } = [];

    /// <summary>
    /// 重试策略；没有则失败即故障。
    /// </summary>
    public WorkflowRetryPolicy? RetryPolicy { get; init; }

    /// <summary>
    /// 挂起后的超时；到期以 <c>NodeTimeout</c> 书签恢复节点。
    /// </summary>
    public TimeSpan? Timeout
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive.");
    }

    /// <summary>
    /// 失败时不故障实例：节点记为故障，变量 <c>lastError</c> 记错误，以 <c>outcome = "error"</c> 流出。
    /// </summary>
    public bool ContinueOnError { get; init; }

    /// <summary>
    /// 显示名或 ID。
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
}
