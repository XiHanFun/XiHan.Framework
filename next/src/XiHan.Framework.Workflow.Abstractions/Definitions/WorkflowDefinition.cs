// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 工作流定义
/// </summary>
/// <remarks>
/// 一张有向图加变量声明；同名多版本，只有已发布的版本能启动实例
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowDefinition
{
    /// <summary>
    /// 定义 ID
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = WorkflowDefinitionRules.IsValidDefinitionId(value) ? value : throw new ArgumentException("The definition ID must not be empty.", nameof(value));
    }

    /// <summary>
    /// 定义名
    /// </summary>
    /// <remarks>
    /// 一个名字下多个版本
    /// </remarks>
    public required WorkflowName Name { get; init; }

    /// <summary>
    /// 版本号，从 1 起
    /// </summary>
    /// <remarks>
    /// JSON 里没写时取初值 1（ADR-0107）
    /// </remarks>
    [JsonIgnore]
    public int Version
    {
        get => field;
        init => field = WorkflowDefinitionRules.IsValidVersion(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The version must be at least 1.");
    } = 1;

    /// <summary>
    /// 序列化用的 <see cref="Version"/>
    /// </summary>
    /// <remarks>
    /// 源生成的序列化器把不在场的 init 成员设成类型的默认值、盖掉初值（ADR-0107）：经这一层可空的成员，不在场或给 <see langword="null"/> 时什么也不赋，定义保留 <see cref="Version"/> 的初值；给了值照常经 <see cref="Version"/> 的校验。写出时总是写出当前值
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("version")]
    internal int? VersionJson
    {
        get => Version;
        init
        {
            if (value is { } given)
            {
                Version = given;
            }
        }
    }

    /// <summary>
    /// 显示名
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 说明
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 分类的最大长度
    /// </summary>
    /// <remarks>
    /// 与数据库实现的分类列同宽（ADR-0096）
    /// </remarks>
    public const int MaxCategoryLength = 128;

    /// <summary>
    /// 分类
    /// </summary>
    /// <remarks>
    /// 定义列表按它精确过滤（<see cref="WorkflowDefinitionQuery.Category"/>）。最多 <see cref="MaxCategoryLength"/> 个字符，超长抛出 <see cref="ArgumentException"/>；从 JSON 读入时 <c>WorkflowJson.DeserializeDefinition</c> 把它转成 <c>WORKFLOW_VALIDATION_001</c>
    /// </remarks>
    public string? Category
    {
        get => field;
        init => field = WorkflowDefinitionRules.IsValidCategory(value) ? value : throw new ArgumentException($"The category must be at most {MaxCategoryLength} characters.", nameof(value));
    }

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowDefinitionStatus Status { get; init; } = WorkflowDefinitionStatus.Draft;

    /// <summary>
    /// 取消实例时是否按完成顺序逆序调用可补偿活动
    /// </summary>
    public bool EnableCompensation { get; init; }

    /// <summary>
    /// 故障时也按完成顺序逆序补偿已完成的节点
    /// </summary>
    public bool CompensateOnFault { get; init; }

    /// <summary>
    /// 实例级超时
    /// </summary>
    /// <remarks>
    /// 启动后超过它即按 <see cref="TimeoutAction"/> 处理；空为不限
    /// </remarks>
    public TimeSpan? Timeout
    {
        get => field;
        init => field = WorkflowDefinitionRules.IsValidTimeout(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive.");
    }

    /// <summary>
    /// 实例级超时到期的处理
    /// </summary>
    /// <remarks>
    /// 默认故障
    /// </remarks>
    public WorkflowTimeoutAction TimeoutAction { get; init; }

    /// <summary>
    /// 节点
    /// </summary>
    /// <remarks>
    /// 总是非空：JSON 里缺省或显式 null 都按空集合处理
    /// </remarks>
    public IReadOnlyList<WorkflowNode> Nodes
    {
        get => field;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// 转移
    /// </summary>
    /// <remarks>
    /// 总是非空：JSON 里缺省或显式 null 都按空集合处理
    /// </remarks>
    public IReadOnlyList<WorkflowTransition> Transitions
    {
        get => field;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// 变量声明
    /// </summary>
    /// <remarks>
    /// 总是非空：JSON 里缺省或显式 null 都按空集合处理
    /// </remarks>
    public IReadOnlyList<WorkflowVariableDefinition> Variables
    {
        get => field;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// 所属租户
    /// </summary>
    /// <remarks>
    /// 平台级为 <see langword="null"/>
    /// </remarks>
    public string? TenantId { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>
    /// 应用扩展属性
    /// </summary>
    public JsonObject? Properties { get; init; }

    /// <summary>
    /// 显示名或名字
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Name.Value : DisplayName;

    /// <summary>
    /// 按 ID 找节点
    /// </summary>
    public Maybe<WorkflowNode> FindNode(string nodeId)
    {
        foreach (var node in Nodes)
        {
            if (string.Equals(node.Id, nodeId, StringComparison.Ordinal))
            {
                return Maybe.Some(node);
            }
        }

        return default;
    }
}
