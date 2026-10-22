// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 回声操作的请求：文本与重复次数。
/// </summary>
/// <param name="Text">要回声的文本；为空时操作返回校验失败。</param>
/// <param name="Repeat">重复次数。</param>
internal sealed record EchoRequest(string? Text, int Repeat = 1);
