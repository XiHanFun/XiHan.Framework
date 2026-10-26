// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流式构造一个节点：属性、指派规则、重试、超时、继续出错。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowNodeBuilder
{
    /// <summary>
    /// 节点 ID。
    /// </summary>
    private readonly string _id;

    /// <summary>
    /// 活动类型。
    /// </summary>
    private readonly ActivityType _activityType;

    /// <summary>
    /// 属性。
    /// </summary>
    private readonly JsonObject _properties = [];

    /// <summary>
    /// 指派规则。
    /// </summary>
    private readonly List<AssigneeRule> _rules = [];

    /// <summary>
    /// 显示名。
    /// </summary>
    private string? _displayName;

    /// <summary>
    /// 重试策略。
    /// </summary>
    private WorkflowRetryPolicy? _retry;

    /// <summary>
    /// 超时。
    /// </summary>
    private TimeSpan? _timeout;

    /// <summary>
    /// 继续出错。
    /// </summary>
    private bool _continueOnError;

    /// <summary>
    /// 输出映射。
    /// </summary>
    private readonly Dictionary<string, string> _outputMappings = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    internal WorkflowNodeBuilder(string id, ActivityType activityType, string? displayName)
    {
        _id = id;
        _activityType = activityType;
        _displayName = displayName;
    }

    /// <summary>
    /// 显示名。
    /// </summary>
    public WorkflowNodeBuilder WithDisplayName(string? displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>
    /// 设属性（深拷贝）。
    /// </summary>
    public WorkflowNodeBuilder WithProperty(string name, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _properties[name] = value?.DeepClone();
        return this;
    }

    /// <summary>
    /// 设字符串属性。
    /// </summary>
    public WorkflowNodeBuilder WithProperty(string name, string? value) => WithProperty(name, value is null ? null : JsonValue.Create(value));

    /// <summary>
    /// 设布尔属性。
    /// </summary>
    public WorkflowNodeBuilder WithProperty(string name, bool value) => WithProperty(name, JsonValue.Create(value));

    /// <summary>
    /// 设数字属性。
    /// </summary>
    public WorkflowNodeBuilder WithProperty(string name, int value) => WithProperty(name, JsonValue.Create(value));

    /// <summary>
    /// 批量设属性（深拷贝，同名覆盖）。
    /// </summary>
    public WorkflowNodeBuilder WithProperties(JsonObject properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        foreach (var (name, value) in properties)
        {
            _properties[name] = value?.DeepClone();
        }

        return this;
    }

    /// <summary>
    /// 加一条指派规则（写进 <c>AssigneeRules</c>）。
    /// </summary>
    public WorkflowNodeBuilder WithAssigneeRule(string type, params IReadOnlyList<string> values)
    {
        _rules.Add(new AssigneeRule(type, values));
        return this;
    }

    /// <summary>
    /// 重试策略。
    /// </summary>
    public WorkflowNodeBuilder WithRetry(int maxAttempts, TimeSpan? firstDelay = null, double backoffFactor = 2.0)
    {
        _retry = new WorkflowRetryPolicy { MaxAttempts = maxAttempts, FirstDelay = firstDelay ?? TimeSpan.FromSeconds(10), BackoffFactor = backoffFactor };
        return this;
    }

    /// <summary>
    /// 挂起后的超时。
    /// </summary>
    public WorkflowNodeBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// 失败时继续（以 <c>outcome = "error"</c> 流出）。
    /// </summary>
    public WorkflowNodeBuilder WithContinueOnError(bool enabled = true)
    {
        _continueOnError = enabled;
        return this;
    }

    /// <summary>
    /// 输出映射：节点完成后以 <c>outputs</c> 为作用域求值 <paramref name="expression"/>，写入变量 <paramref name="variable"/>。
    /// </summary>
    public WorkflowNodeBuilder WithOutputMapping(string variable, string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        _outputMappings[variable] = expression;
        return this;
    }

    /// <summary>
    /// 生成节点。
    /// </summary>
    internal WorkflowNode Build()
    {
        var properties = (JsonObject)_properties.DeepClone();
        if (_rules.Count > 0)
        {
            properties[UserTaskActivity.AssigneeRulesProperty] = new JsonArray(_rules.Select(static rule => (JsonNode?)rule.ToJson()).ToArray());
        }

        return new WorkflowNode
        {
            Id = _id,
            ActivityType = _activityType,
            DisplayName = _displayName,
            Properties = properties,
            RetryPolicy = _retry,
            Timeout = _timeout,
            ContinueOnError = _continueOnError,
            OutputMappings = _outputMappings.Count == 0 ? null : new Dictionary<string, string>(_outputMappings, StringComparer.Ordinal),
        };
    }
}
