// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义里的一个节点。
/// </summary>
/// <remarks>
/// 活动类型加属性；字符串属性支持 <c>{{ 表达式 }}</c> 模板。
/// </remarks>
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
    /// 显示名。
    /// </summary>
    /// <remarks>
    /// 缺省用 ID。
    /// </remarks>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 活动属性。
    /// </summary>
    /// <remarks>
    /// 各活动类型自行解释。
    /// 总是非空：JSON 里缺省或显式 null 都按空属性处理（源生成的反序列化对 init 属性逐个赋值，缺省项会传 null）。
    /// </remarks>
    public JsonObject Properties
    {
        get => field;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// 重试策略。
    /// </summary>
    /// <remarks>
    /// 没有则失败即故障。
    /// </remarks>
    public WorkflowRetryPolicy? RetryPolicy { get; init; }

    /// <summary>
    /// 挂起后的超时。
    /// </summary>
    /// <remarks>
    /// 到期以 <c>NodeTimeout</c> 书签恢复节点。
    /// </remarks>
    public TimeSpan? Timeout
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive.");
    }

    /// <summary>
    /// 失败时不故障实例。
    /// </summary>
    /// <remarks>
    /// 节点记为故障，变量 <c>lastError</c> 记错误，以 <c>outcome = "error"</c> 流出。
    /// </remarks>
    public bool ContinueOnError { get; init; }

    /// <summary>
    /// 输出映射。
    /// </summary>
    /// <remarks>
    /// 变量名 → 表达式，节点完成后以 <c>outputs</c>（本节点输出）为作用域求值并写入实例变量；求值失败即节点故障。
    /// </remarks>
    public IReadOnlyDictionary<string, string>? OutputMappings { get; init; }

    /// <summary>
    /// 显示名或 ID。
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
}
