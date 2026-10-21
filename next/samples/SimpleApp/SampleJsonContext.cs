// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace SimpleApp;

[JsonSerializable(typeof(Greeting))]
[JsonSerializable(typeof(WhoAmI))]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
[JsonSerializable(typeof(List<OrderResponse>))]
[JsonSerializable(typeof(OrderStatistics))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;
