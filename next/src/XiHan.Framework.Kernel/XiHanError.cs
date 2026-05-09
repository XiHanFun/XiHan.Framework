// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel;

public sealed record XiHanError
{
    public string Code { get; }
    public string Message { get; }
    public Exception? InnerException { get; }

    public XiHanError(string code, string message, Exception? innerException = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Message = message;
        InnerException = innerException;
    }

    public static XiHanError Unexpected(string message, Exception? innerException = null)
        => new("UNEXPECTED", message, innerException);

    public static XiHanError Validation(string message)
        => new("VALIDATION", message);

    public static XiHanError NotFound(string message)
        => new("NOT_FOUND", message);

    public override string ToString() => $"[{Code}] {Message}";
}
