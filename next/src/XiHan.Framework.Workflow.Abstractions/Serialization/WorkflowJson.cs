// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Serialization;

/// <summary>
/// 工作流模型的 JSON 往返与深拷贝（源生成，AOT 可用）：存储以此持久化，设计器以此交换定义。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowJson
{
    /// <summary>
    /// 定义 → JSON。
    /// </summary>
    public static string Serialize(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonSerializer.Serialize(definition, WorkflowJsonContext.Default.WorkflowDefinition);
    }

    /// <summary>
    /// JSON → 定义；不合法返回 <see cref="WorkflowErrors.DefinitionInvalid"/>。
    /// </summary>
    public static Result<WorkflowDefinition> DeserializeDefinition(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var definition = JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowDefinition);
            return definition is null
                ? Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid, MessageArguments.Of(("errors", "null")))
                : definition;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid, MessageArguments.Of(("errors", exception.Message)));
        }
    }

    /// <summary>
    /// 实例 → JSON。
    /// </summary>
    public static string Serialize(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return JsonSerializer.Serialize(instance, WorkflowJsonContext.Default.WorkflowInstance);
    }

    /// <summary>
    /// JSON → 实例；不合法抛出 <see cref="JsonException"/>。
    /// </summary>
    public static WorkflowInstance DeserializeInstance(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowInstance) ?? throw new JsonException("The instance JSON is null.");
    }

    /// <summary>
    /// 节点实例 → JSON。
    /// </summary>
    public static string Serialize(WorkflowNodeInstance nodeInstance)
    {
        ArgumentNullException.ThrowIfNull(nodeInstance);
        return JsonSerializer.Serialize(nodeInstance, WorkflowJsonContext.Default.WorkflowNodeInstance);
    }

    /// <summary>
    /// JSON → 节点实例；不合法抛出 <see cref="JsonException"/>。
    /// </summary>
    public static WorkflowNodeInstance DeserializeNodeInstance(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowNodeInstance) ?? throw new JsonException("The node instance JSON is null.");
    }

    /// <summary>
    /// 书签 → JSON。
    /// </summary>
    public static string Serialize(WorkflowBookmark bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        return JsonSerializer.Serialize(bookmark, WorkflowJsonContext.Default.WorkflowBookmark);
    }

    /// <summary>
    /// JSON → 书签；不合法抛出 <see cref="JsonException"/>。
    /// </summary>
    public static WorkflowBookmark DeserializeBookmark(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowBookmark) ?? throw new JsonException("The bookmark JSON is null.");
    }

    /// <summary>
    /// 深拷贝定义（含里面的 JSON 值）。
    /// </summary>
    public static WorkflowDefinition Clone(WorkflowDefinition definition) => DeserializeDefinition(Serialize(definition)).ValueOrThrow();

    /// <summary>
    /// 深拷贝实例。
    /// </summary>
    public static WorkflowInstance Clone(WorkflowInstance instance) => DeserializeInstance(Serialize(instance));

    /// <summary>
    /// 深拷贝节点实例。
    /// </summary>
    public static WorkflowNodeInstance Clone(WorkflowNodeInstance nodeInstance) => DeserializeNodeInstance(Serialize(nodeInstance));

    /// <summary>
    /// 深拷贝书签。
    /// </summary>
    public static WorkflowBookmark Clone(WorkflowBookmark bookmark) => DeserializeBookmark(Serialize(bookmark));

    /// <summary>
    /// 深拷贝 JSON 对象；<see langword="null"/> 得到空对象。
    /// </summary>
    public static JsonObject CloneObject(JsonObject? value) => value is null ? [] : (JsonObject)value.DeepClone();

    /// <summary>
    /// 深拷贝 JSON 值。
    /// </summary>
    public static JsonNode? CloneNode(JsonNode? value) => value?.DeepClone();

    /// <summary>
    /// 事件数据 → UTF-8 JSON。
    /// </summary>
    public static byte[] SerializeEvent(WorkflowEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return JsonSerializer.SerializeToUtf8Bytes(data, WorkflowJsonContext.Default.WorkflowEventData);
    }

    /// <summary>
    /// UTF-8 JSON → 事件数据；不合法抛出 <see cref="JsonException"/>。
    /// </summary>
    public static WorkflowEventData DeserializeEvent(ReadOnlySpan<byte> json)
        => JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowEventData) ?? throw new JsonException("The event JSON is null.");
}
