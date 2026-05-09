// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Kernel;

/// <summary>
/// <see cref="XiHanResult{T}"/> 的静态工厂方法。
/// </summary>
public static class XiHanResult
{
    /// <summary>
    /// 创建一个成功的 Result。
    /// </summary>
    public static XiHanResult<T> Success<T>(T value) => new(value);

    /// <summary>
    /// 创建一个失败的 Result。
    /// </summary>
    public static XiHanResult<T> Failure<T>(XiHanError error) => new(error);
}

/// <summary>
/// 表示可能成功或失败的通用操作结果。
/// 成功时包含值，失败时包含 <see cref="XiHanError"/>。
/// </summary>
public readonly struct XiHanResult<T>
{
    private readonly T? _value;
    private readonly XiHanError? _error;

    internal XiHanResult(T value) { _value = value; _error = null; IsSuccess = true; }
    internal XiHanResult(XiHanError error) { _value = default; _error = error; IsSuccess = false; }

    /// <summary>
    /// 操作是否成功。
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// 操作是否失败。
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 成功时的值。仅在 <see cref="IsSuccess"/> 为 <c>true</c> 时安全访问。
    /// </summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Cannot access Value. Error: {_error}");

    /// <summary>
    /// 失败时的错误。仅在 <see cref="IsFailure"/> 为 <c>true</c> 时安全访问。
    /// </summary>
    public XiHanError Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on success.");

    /// <summary>
    /// 如果成功则转换值。
    /// </summary>
    public XiHanResult<TOut> Map<TOut>(Func<T, TOut> mapper)
        => IsSuccess ? XiHanResult.Success(mapper(_value!)) : XiHanResult.Failure<TOut>(_error!);

    /// <summary>
    /// 如果成功则执行可能失败的操作。
    /// </summary>
    public XiHanResult<TOut> Bind<TOut>(Func<T, XiHanResult<TOut>> binder)
        => IsSuccess ? binder(_value!) : XiHanResult.Failure<TOut>(_error!);

    /// <summary>
    /// 如果失败则用工厂函数替换错误。
    /// </summary>
    public XiHanResult<T> MapError(Func<XiHanError, XiHanError> mapper)
        => IsFailure ? XiHanResult.Failure<T>(mapper(_error!)) : this;

    /// <summary>
    /// 解构 Result，同时处理成功和失败路径。
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<XiHanError, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    /// <summary>
    /// 隐式转换：从值创建成功的 Result。
    /// </summary>
    public static implicit operator XiHanResult<T>(T value) => XiHanResult.Success(value);

    /// <summary>
    /// 隐式转换：从 XiHanError 创建失败的 Result。
    /// </summary>
    public static implicit operator XiHanResult<T>(XiHanError error) => XiHanResult.Failure<T>(error);

    /// <inheritdoc />
    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}
