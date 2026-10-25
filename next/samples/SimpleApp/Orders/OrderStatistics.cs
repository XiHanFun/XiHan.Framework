// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp.Orders;

/// <summary>
/// <c>/orders/stats</c> 端点的响应。
/// </summary>
/// <param name="Handled">已处理的订单事件数。</param>
public sealed record OrderStatistics(long Handled);
