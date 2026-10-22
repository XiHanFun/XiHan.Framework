// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

/// <summary>
/// 订单端点的响应，也是 <c>orders.placed</c> 消息的正文。
/// </summary>
/// <param name="Id">订单 ID。</param>
/// <param name="Name">订单名称。</param>
/// <param name="Amount">订单金额。</param>
/// <param name="CreatedAt">创建时间。</param>
public sealed record OrderResponse(Guid Id, string Name, int Amount, DateTimeOffset CreatedAt);
