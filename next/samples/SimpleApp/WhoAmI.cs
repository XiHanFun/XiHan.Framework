// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

/// <summary>
/// <c>/whoami</c> 端点的响应：当前调用的安全上下文摘要。
/// </summary>
/// <param name="Principal">主体 ID；匿名时为匿名主体的 ID。</param>
/// <param name="IsAuthenticated">是否已认证。</param>
/// <param name="Tenant">当前租户 ID；未解析出租户时为 <c>null</c>。</param>
public sealed record WhoAmI(string Principal, bool IsAuthenticated, string? Tenant);
