// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Events;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 测试事件：订单已发货。
/// </summary>
/// <param name="OrderId">订单 ID。</param>
/// <param name="OccurredAt">发生时间。</param>
internal sealed record OrderShippedEvent(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;
