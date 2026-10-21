// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;

namespace SimpleApp;

public sealed record OrderPlaced(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;
