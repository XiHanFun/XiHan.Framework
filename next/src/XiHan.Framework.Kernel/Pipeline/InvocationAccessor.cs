// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// <see cref="IInvocationAccessor"/> 的内核实现。
/// </summary>
/// <remarks>
/// 作用域内一个实例，<see cref="Hosting.App.InvokeAsync"/> 进入管道前 <see cref="Enter"/>、离开时恢复。
/// </remarks>
internal sealed class InvocationAccessor : IInvocationAccessor
{
    /// <summary>
    /// 当前上下文。
    /// </summary>
    /// <remarks>
    /// 不在调用内为 <see langword="null"/>。
    /// </remarks>
    private InvocationContext? _current;

    /// <inheritdoc />
    public Maybe<InvocationContext> Current => _current is null ? default : Maybe.Some(_current);

    /// <summary>
    /// 把 <paramref name="context"/> 设为当前上下文，返回的作用域释放时恢复之前的值。
    /// </summary>
    /// <remarks>
    /// 同一服务作用域内的嵌套调用据此形成栈。
    /// </remarks>
    public Scope Enter(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = _current;
        _current = context;
        return new Scope(this, previous);
    }

    /// <summary>
    /// 一次 <see cref="Enter"/> 的作用域。
    /// </summary>
    /// <remarks>
    /// 默认值释放时什么都不做。
    /// </remarks>
    public readonly struct Scope(InvocationAccessor? accessor, InvocationContext? previous) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            if (accessor is not null)
            {
                accessor._current = previous;
            }
        }
    }
}
