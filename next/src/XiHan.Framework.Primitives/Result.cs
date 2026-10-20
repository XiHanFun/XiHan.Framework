// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 不携带值的操作结果：成功，或携带 <see cref="Error"/> 的失败。
/// <c>default</c> 值不是合法结果，读取时视为 <see cref="ErrorCode.UninitializedResult"/> 失败。
/// 线上编码：<c>{"ok":true}</c> 或 <c>{"ok":false,"error":{…}}</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct Result
{
    internal static readonly Error Uninitialized = new(ErrorCode.UninitializedResult, "Result 未初始化：default 值不是合法结果。");

    private readonly bool _isSuccess;
    private readonly Error? _error;

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
    /// 失败时的错误；成功时为 <see langword="null"/>。
    /// </summary>
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
    /// 创建携带值的成功结果；值不能为 <see langword="null"/>。
    /// </summary>
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
