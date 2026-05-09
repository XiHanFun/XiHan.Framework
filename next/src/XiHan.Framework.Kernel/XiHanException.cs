// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel;

/// <summary>
/// XiHan 框架基础异常。
/// 所有框架内部抛出的异常都继承自此类型。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public class XiHanException : Exception
{
    /// <summary>
    /// 创建一个框架异常。
    /// </summary>
    public XiHanException(string code, string message) : base(message) => Code = code;

    /// <summary>
    /// 创建一个包含内部异常的框架异常。
    /// </summary>
    public XiHanException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;

    /// <summary>
    /// 错误码。
    /// </summary>
    public string Code { get; }

    /// <inheritdoc />
    public override string ToString() => $"[{Code}] {base.ToString()}";
}
