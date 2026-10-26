// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 测试事件的源生成序列化上下文（camelCase）。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OrderCreatedEvent))]
[JsonSerializable(typeof(OrderShippedEvent))]
internal sealed partial class DomainEventJsonContext : JsonSerializerContext;
