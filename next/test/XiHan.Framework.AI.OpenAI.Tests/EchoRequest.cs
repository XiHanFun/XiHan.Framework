// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// 回显请求。
/// </summary>
internal sealed record EchoRequest(string Text, int Repeat = 1);
