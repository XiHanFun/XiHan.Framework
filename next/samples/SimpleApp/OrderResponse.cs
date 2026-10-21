// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

public sealed record OrderResponse(Guid Id, string Name, int Amount, DateTimeOffset CreatedAt);
