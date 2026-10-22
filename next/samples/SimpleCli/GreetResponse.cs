// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleCli;

/// <summary>
/// 问候响应。
/// </summary>
/// <param name="Message">问候语。</param>
/// <param name="TraceId">本次调用的 trace-id。</param>
public sealed record GreetResponse(string Message, string TraceId);
