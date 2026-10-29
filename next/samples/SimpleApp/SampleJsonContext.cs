// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using SimpleApp.Greetings;
using SimpleApp.Orders;

namespace SimpleApp;

/// <summary>
/// 示例端点请求与响应类型的源生成序列化上下文。
/// </summary>
/// <remarks>
/// 示例以 AOT 发布做冒烟，所有 JSON 类型都要登记在这里。
/// </remarks>
// camelCase 属性名与不区分大小写：HTTP 适配器把路由值、查询串与请求体合并成 JSON 对象交给操作，属性名按 JSON 约定匹配；
// 允许从字符串读数字：路由值与查询串都是字符串
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(Greeting))]
[JsonSerializable(typeof(WhoAmI))]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
[JsonSerializable(typeof(OrderPlaced))]
[JsonSerializable(typeof(ListOrdersRequest))]
[JsonSerializable(typeof(OrderList))]
[JsonSerializable(typeof(OrderStatistics))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;
