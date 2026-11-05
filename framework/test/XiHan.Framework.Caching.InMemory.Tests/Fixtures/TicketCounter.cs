// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Caching.InMemory.Tests.Fixtures;

/// <summary>
/// 取号操作的执行计数（单例）
/// </summary>
internal sealed class TicketCounter
{
    /// <summary>
    /// 已执行次数
    /// </summary>
    private int _executions;

    /// <summary>
    /// 操作已执行的次数
    /// </summary>
    public int Executions => Volatile.Read(ref _executions);

    /// <summary>
    /// 记一次执行并返回新的号码
    /// </summary>
    public int Next() => Interlocked.Increment(ref _executions);
}
