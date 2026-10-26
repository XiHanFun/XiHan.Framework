// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Security.Database.Tests.Fixtures;

/// <summary>
/// 写订单操作的响应：处理它的主体 ID 与当前租户。
/// </summary>
/// <param name="Principal">主体 ID。</param>
/// <param name="Tenant">当前租户；无租户为 <see langword="null"/>。</param>
internal sealed record OrderResponse(string Principal, string? Tenant);
