// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 携带值的操作结果。
/// </summary>
/// <remarks>
/// 成功时携带非空的 <typeparamref name="T"/>，失败时携带 <see cref="Error"/>。用 <see cref="Result.Success{T}(T)"/> 与 <see cref="Result.Failure{T}(Error)"/> 创建，或直接从值与错误隐式转换。成功值不允许为 <see langword="null"/>；"成功但没有值"请用 <see cref="Result"/> 或 <see cref="Result{T}"/> 包 <see cref="Maybe{T}"/>。<c>default</c> 值不是合法结果，读取时视为 <see cref="ErrorCode.UninitializedResult"/> 失败。线上编码：<c>{"ok":true,"value":…}</c> 或 <c>{"ok":false,"error":{…}}</c>。
/// </remarks>
/// <typeparam name="T">值类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct Result<T>
{
    /// <summary>
    /// 是否成功。
    /// </summary>
    /// <remarks>
    /// <c>default</c> 实例为 <see langword="false"/>，因此 default 天然落入失败分支。
    /// </remarks>
    private readonly bool _isSuccess;

    /// <summary>
    /// 成功时的值。
    /// </summary>
    /// <remarks>
    /// 失败时为类型默认值，不得读取。
    /// </remarks>
    private readonly T? _value;

    /// <summary>
    /// 失败时的错误。
    /// </summary>
    /// <remarks>
    /// <c>default</c> 实例为 <see langword="null"/>，读取时以 <see cref="Result.Uninitialized"/> 代替。
    /// </remarks>
    private readonly Error? _error;

    /// <summary>
    /// 创建结果。
    /// </summary>
    /// <remarks>
    /// 只供 <see cref="Result"/> 的工厂方法与本类型的组合方法调用，保证成功时值非空、失败时有错误。
    /// </remarks>
    /// <param name="isSuccess">是否成功。</param>
    /// <param name="value">成功时的值。</param>
    /// <param name="error">失败时的错误。</param>
    internal Result(bool isSuccess, T? value, Error? error)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// 是否失败。
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// 失败时的错误。
    /// </summary>
    /// <remarks>
    /// 成功时为 <see langword="null"/>。
    /// </remarks>
    public Error? Error => _isSuccess ? null : _error ?? Result.Uninitialized;

    /// <summary>
    /// 把值隐式转换为成功结果。
    /// </summary>
    /// <remarks>
    /// 值不能为 <see langword="null"/>。
    /// </remarks>
    public static implicit operator Result<T>(T value) => Result.Success(value);

    /// <summary>
    /// 把错误隐式转换为失败结果。
    /// </summary>
    public static implicit operator Result<T>(Error error) => Result.Failure<T>(error);

    /// <summary>
    /// 成功时取值。
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _isSuccess;
    }

    /// <summary>
    /// 成功时返回值，失败时抛出携带错误码的 <see cref="FrameworkException"/>。
    /// </summary>
    public T ValueOrThrow()
    {
        if (_isSuccess)
        {
            // 成功结果由 Result.Success(value) 构造，已拒绝 null，此处只是把 T? 收窄回 T。
            return _value!;
        }

        var error = _error ?? Result.Uninitialized;
        throw new FrameworkException(error.Code, error.Message);
    }

    /// <summary>
    /// 成功时返回值，失败时返回 <paramref name="fallback"/>。
    /// </summary>
    public T ValueOr(T fallback) => _isSuccess ? _value! : fallback;

    /// <summary>
    /// 按成功或失败分支求值。
    /// </summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value!) : onFailure(_error ?? Result.Uninitialized);
    }

    /// <summary>
    /// 成功时映射值，失败时原样传递错误。
    /// </summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        // 失败分支显式带上 Uninitialized：default 结果经 Map 后仍是可解释的失败，而不是又一个 default。
        return _isSuccess ? Result.Success(map(_value!)) : new Result<TOut>(false, default, _error ?? Result.Uninitialized);
    }

    /// <summary>
    /// 成功时继续执行 <paramref name="bind"/>，失败时原样传递错误。
    /// </summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _isSuccess ? bind(_value!) : new Result<TOut>(false, default, _error ?? Result.Uninitialized);
    }

    /// <summary>
    /// 丢弃值，转换为不携带值的 <see cref="Result"/>。
    /// </summary>
    public Result ToResult() => _isSuccess ? Result.Success() : Result.Failure(_error ?? Result.Uninitialized);

    /// <inheritdoc />
    public override string ToString() => _isSuccess ? $"Success({_value})" : $"Failure({_error ?? Result.Uninitialized})";
}
