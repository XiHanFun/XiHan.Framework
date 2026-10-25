// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp.Greetings;

/// <summary>
/// 已处理的调用次数。
/// </summary>
public sealed class GreetingCounter
{
    /// <summary>
    /// 计数值；只通过 <see cref="Interlocked"/> 读写，多个请求并发时也准确。
    /// </summary>
    private long _count;

    /// <summary>
    /// 当前计数。
    /// </summary>
    public long Count => Interlocked.Read(ref _count);

    /// <summary>
    /// 计数加一。
    /// </summary>
    public void Increment() => Interlocked.Increment(ref _count);
}
