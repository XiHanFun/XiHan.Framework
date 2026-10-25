// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp.Greetings;

/// <summary>
/// <c>/greet</c> 端点的响应。
/// </summary>
/// <param name="Message">问候语。</param>
/// <param name="Served">到目前为止经过管道的调用次数。</param>
public sealed record Greeting(string Message, long Served);
