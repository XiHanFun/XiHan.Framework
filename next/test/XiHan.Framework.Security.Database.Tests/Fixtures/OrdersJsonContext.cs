// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Security.Database.Tests.Fixtures;

/// <summary>
/// 写订单操作请求与响应的源生成序列化上下文。
/// </summary>
[JsonSerializable(typeof(OrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
internal sealed partial class OrdersJsonContext : JsonSerializerContext;
