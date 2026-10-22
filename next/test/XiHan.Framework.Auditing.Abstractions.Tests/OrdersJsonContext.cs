// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 测试操作的序列化上下文。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateOrderRequest))]
[JsonSerializable(typeof(ListOrdersRequest))]
[JsonSerializable(typeof(OrderResponse))]
internal sealed partial class OrdersJsonContext : JsonSerializerContext;
