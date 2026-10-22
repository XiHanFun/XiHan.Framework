// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace SimpleApp;

/// <summary>
/// 示例端点请求与响应类型的源生成序列化上下文；示例以 AOT 发布做冒烟，所有 JSON 类型都要登记在这里。
/// </summary>
[JsonSerializable(typeof(Greeting))]
[JsonSerializable(typeof(WhoAmI))]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
[JsonSerializable(typeof(List<OrderResponse>))]
[JsonSerializable(typeof(OrderStatistics))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;
