// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel;

/// <summary>
/// 框架基础异常。所有 XiHan 框架抛出的异常都继承自此类型。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public class XiHanException : Exception
{
    /// <summary>
    /// 错误码。
    /// </summary>
    public string Code { get; }

    public XiHanException(string code, string message) : base(message)
    {
        Code = code;
    }

    public XiHanException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }

    public override string ToString() => $"[{Code}] {Message}";
}
