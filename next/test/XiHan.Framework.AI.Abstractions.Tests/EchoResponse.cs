// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 回显响应：文本与执行它的调用的追踪 ID。
/// </summary>
internal sealed record EchoResponse(string Echo, string TraceId, string? Tenant);
