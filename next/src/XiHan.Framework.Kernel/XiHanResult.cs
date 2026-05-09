// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Kernel;

public readonly struct XiHanResult<T>
{
    private readonly T? _value;
    private readonly XiHanError? _error;

    private XiHanResult(T value)
    { _value = value; _error = null; IsSuccess = true; }

    private XiHanResult(XiHanError error)
    { _value = default; _error = error; IsSuccess = false; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Cannot access Value. Error: {_error}");
    public XiHanError Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on success.");

    public static XiHanResult<T> Success(T value) => new(value);

    public static XiHanResult<T> Failure(XiHanError error) => new(error);

    public XiHanResult<TOut> Map<TOut>(Func<T, TOut> mapper)
        => IsSuccess ? XiHanResult<TOut>.Success(mapper(_value!)) : XiHanResult<TOut>.Failure(_error!);

    public XiHanResult<TOut> Bind<TOut>(Func<T, XiHanResult<TOut>> binder)
        => IsSuccess ? binder(_value!) : XiHanResult<TOut>.Failure(_error!);

    public XiHanResult<T> MapError(Func<XiHanError, XiHanError> mapper)
        => IsFailure ? Failure(mapper(_error!)) : this;

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<XiHanError, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public static implicit operator XiHanResult<T>(T value) => Success(value);

    public static implicit operator XiHanResult<T>(XiHanError error) => Failure(error);

    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}
