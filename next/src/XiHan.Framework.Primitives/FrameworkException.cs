// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 框架异常基类，携带错误码。异常只用于不可恢复或编程错误；可恢复失败用 <see cref="Result"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public class FrameworkException : Exception
{
    /// <summary>
    /// 创建异常。
    /// </summary>
    /// <param name="code">符合 <see cref="ErrorCode"/> 格式的错误码。</param>
    /// <param name="message">面向人的说明。</param>
    /// <param name="innerException">内部异常。</param>
    public FrameworkException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = ErrorCode.Validate(code);
    }

    /// <summary>
    /// 错误码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 转换为 <see cref="Error"/>；内部异常若也是 <see cref="FrameworkException"/> 则成为 <see cref="Error.Inner"/>。
    /// </summary>
    public Error ToError() => new(Code, Message, null, InnerException is FrameworkException inner ? inner.ToError() : null);
}
