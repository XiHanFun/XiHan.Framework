// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Messaging.Abstractions.CloudEvents;

/// <summary>
/// CloudEvents 文档的源生成序列化上下文。
/// </summary>
/// <remarks>
/// 写出时忽略 <see langword="null"/> 成员。
/// </remarks>
[JsonSerializable(typeof(CloudEventDocument))]
[JsonSerializable(typeof(string))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class MessagingJsonContext : JsonSerializerContext;
