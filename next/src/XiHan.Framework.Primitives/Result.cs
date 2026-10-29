// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 不携带值的操作结果。
/// </summary>
/// <remarks>
/// 成功，或携带 <see cref="Error"/> 的失败。<c>default</c> 值不是合法结果，读取时视为 <see cref="ErrorCode.UninitializedResult"/> 失败。线上编码：<c>{"ok":true}</c> 或 <c>{"ok":false,"error":{…}}</c>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct Result
{
    /// <summary>
    /// <c>default</c> 结果被读取时统一返回的错误。
    /// </summary>
    /// <remarks>
    /// <see cref="Result{T}"/> 复用同一实例。
    /// </remarks>
    internal static readonly Error Uninitialized = new(ErrorCode.UninitializedResult, ResultMessages.Uninitialized);

    /// <summary>
    /// 是否成功。
    /// </summary>
    /// <remarks>
    /// <c>default</c> 实例为 <see langword="false"/>，因此 default 天然落入失败分支。
    /// </remarks>
    private readonly bool _isSuccess;

    /// <summary>
    /// 失败时的错误。
    /// </summary>
    /// <remarks>
    /// <c>default</c> 实例为 <see langword="null"/>，读取时以 <see cref="Uninitialized"/> 代替。
    /// </remarks>
    private readonly Error? _error;

    /// <summary>
    /// 创建结果。
    /// </summary>
    /// <remarks>
    /// 只供工厂方法调用，保证成功时无错误、失败时有错误。
    /// </remarks>
    /// <param name="isSuccess">是否成功。</param>
    /// <param name="error">失败时的错误。</param>
    private Result(bool isSuccess, Error? error)
    {
        _isSuccess = isSuccess;
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
    public Error? Error => _isSuccess ? null : _error ?? Uninitialized;

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }

    /// <summary>
    /// 创建携带值的成功结果。
    /// </summary>
    /// <remarks>
    /// 值不能为 <see langword="null"/>。
    /// </remarks>
    public static Result<T> Success<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(true, value, null);
    }

    /// <summary>
    /// 创建携带值类型的失败结果。
    /// </summary>
    public static Result<T> Failure<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }

    /// <summary>
    /// 把错误隐式转换为失败结果。
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// 按成功或失败分支求值。
    /// </summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess() : onFailure(_error ?? Uninitialized);
    }

    /// <summary>
    /// 成功时继续执行 <paramref name="next"/>，失败时原样返回。
    /// </summary>
    public Result Bind(Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return _isSuccess ? next() : this;
    }

    /// <summary>
    /// 失败时抛出携带错误码的 <see cref="FrameworkException"/>。
    /// </summary>
    public void ThrowIfFailure()
    {
        if (!_isSuccess)
        {
            var error = _error ?? Uninitialized;
            throw new FrameworkException(error.Code, error.Message);
        }
    }

    /// <inheritdoc />
    public override string ToString() => _isSuccess ? "Success" : $"Failure({_error ?? Uninitialized})";
}
