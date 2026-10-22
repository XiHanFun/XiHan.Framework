// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Protocols.Grpc.Tests;

/// <summary>
/// 订单请求与响应的源生成序列化上下文：camelCase 属性名。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OrderRequest))]
[JsonSerializable(typeof(OrderReply))]
internal sealed partial class OrdersJsonContext : JsonSerializerContext;
