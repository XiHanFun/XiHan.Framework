// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 一次在途的回源，交给执行回源的调用用来核对它是否还作数
/// </summary>
/// <remarks>
/// <see cref="InFlightLoadTable.RunAsync"/> 把它传给回源函数。回源期间这个键被写入或删除（<see cref="InFlightLoadTable.Supersede(Stores.CacheKey)"/>）时它被取代：回源照常结束、结果照常交给已经在等的调用，但结果可能早于那次变更，不应再写回缓存。
/// 进程内的表用 <see cref="TryWrite"/> 写回（与取代互斥，结论是确定的）；异步的后端在发出写入之前看 <see cref="IsSuperseded"/>（已经发出的写入撤不回，先后由后端决定）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class InFlightLoad
{
    /// <summary>
    /// 让写回与取代互斥
    /// </summary>
    private readonly Lock _writeGate = new();

    /// <summary>
    /// 是否已被取代
    /// </summary>
    /// <remarks>
    /// 只在 <see cref="_writeGate"/> 内由假变真，读取不加锁
    /// </remarks>
    private volatile bool _superseded;

    /// <summary>
    /// 只允许本程序集派生
    /// </summary>
    private protected InFlightLoad()
    {
    }

    /// <summary>
    /// 回源期间这个键是否被写入或删除过
    /// </summary>
    /// <remarks>
    /// 为真后不会再变回假
    /// </remarks>
    public bool IsSuperseded => _superseded;

    /// <summary>
    /// 在调用链上认出这次回源用的标识
    /// </summary>
    /// <remarks>
    /// 调用链随执行上下文流进回源函数启动的一切（计时器、后台任务），可能活得比这次回源久。链上只放这个标识、不放回源本身，回源的结果不会被它们留住
    /// </remarks>
    internal object Identity { get; } = new();

    /// <summary>
    /// 没有被取代时执行同步的写回
    /// </summary>
    /// <remarks>
    /// 核对与写回在同一个临界区里，取代在临界区之外等它结束：取代返回之后这次回源不会再写；在那之前写下的，随后的写入或删除会盖掉它。<paramref name="write"/> 里不要再进本表
    /// </remarks>
    /// <returns>执行了写回为 <see langword="true"/>，已被取代为 <see langword="false"/></returns>
    public bool TryWrite<TState>(TState state, Action<TState> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        lock (_writeGate)
        {
            if (_superseded)
            {
                return false;
            }

            write(state);
            return true;
        }
    }

    /// <summary>
    /// 标记为已被取代
    /// </summary>
    /// <remarks>
    /// 正在进行的 <see cref="TryWrite"/> 结束之后才返回
    /// </remarks>
    internal void Supersede()
    {
        lock (_writeGate)
        {
            _superseded = true;
        }
    }
}
