// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.


namespace SimpleApp.Orders;

/// <summary>
/// 当前租户的订单列表。
/// </summary>
/// <param name="Items">按创建时间排序的订单。</param>
public sealed record OrderList(IReadOnlyList<OrderResponse> Items);
