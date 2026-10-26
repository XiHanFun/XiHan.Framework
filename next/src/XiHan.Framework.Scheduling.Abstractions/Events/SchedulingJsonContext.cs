// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Scheduling.Abstractions.Events;

/// <summary>
/// 调度事件数据的源生成序列化上下文（AOT 可用）。
/// </summary>
[JsonSerializable(typeof(SchedulingEventData))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
internal sealed partial class SchedulingJsonContext : JsonSerializerContext;
