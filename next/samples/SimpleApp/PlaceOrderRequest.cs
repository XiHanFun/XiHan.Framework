// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

/// <summary>
/// <c>POST /orders</c> 的请求体。
/// </summary>
/// <param name="Name">订单名称。</param>
/// <param name="Amount">订单金额。</param>
public sealed record PlaceOrderRequest(string Name, int Amount);
