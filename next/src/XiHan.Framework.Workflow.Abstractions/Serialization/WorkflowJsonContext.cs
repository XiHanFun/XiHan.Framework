// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Serialization;

/// <summary>
/// 工作流模型的源生成序列化上下文。
/// </summary>
/// <remarks>
/// 定义、实例、节点实例、书签、事件数据与 JSON 值（AOT 可用）。
/// </remarks>
[JsonSerializable(typeof(WorkflowDefinition))]
[JsonSerializable(typeof(WorkflowInstance))]
[JsonSerializable(typeof(WorkflowNodeInstance))]
[JsonSerializable(typeof(WorkflowBookmark))]
[JsonSerializable(typeof(WorkflowEventData))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSerializable(typeof(JsonNode))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class WorkflowJsonContext : JsonSerializerContext;
