// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;

namespace SimpleApp;

/// <summary>
/// 订单已下单的领域事件。
/// </summary>
/// <param name="OrderId">订单 ID。</param>
/// <param name="OccurredAt">下单时间。</param>
public sealed record OrderPlaced(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;
