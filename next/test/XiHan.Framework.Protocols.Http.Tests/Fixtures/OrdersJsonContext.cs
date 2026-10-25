// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 订单请求与响应的源生成序列化上下文：camelCase 属性名，允许从字符串读数字（路由值与查询串都是字符串）。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(OrderRequest))]
[JsonSerializable(typeof(OrderReportRequest))]
[JsonSerializable(typeof(OrderFilterRequest))]
[JsonSerializable(typeof(OrderSearchRequest))]
[JsonSerializable(typeof(OrderReply))]
internal sealed partial class OrdersJsonContext : JsonSerializerContext;
