// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel;

/// <summary>
/// XiHan 框架基础错误。
/// </summary>
public sealed record XiHanError
{
    /// <summary>
    /// 错误码
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 错误描述。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 可选的内部异常。
    /// </summary>
    public Exception? InnerException { get; }

    /// <summary>
    /// 创建一个错误。
    /// </summary>
    public XiHanError(string code, string message, Exception? innerException = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Message = message;
        InnerException = innerException;
    }

    /// <summary>
    /// 创建一个未预期的内部错误。
    /// </summary>
    public static XiHanError Unexpected(string message, Exception? innerException = null)
        => new("UNEXPECTED", message, innerException);

    /// <summary>
    /// 创建一个验证错误。
    /// </summary>
    public static XiHanError Validation(string message)
        => new("VALIDATION", message);

    /// <summary>
    /// 创建一个未找到错误。
    /// </summary>
    public static XiHanError NotFound(string message)
        => new("NOT_FOUND", message);

    /// <inheritdoc />
    public override string ToString() => $"[{Code}] {Message}";
}
