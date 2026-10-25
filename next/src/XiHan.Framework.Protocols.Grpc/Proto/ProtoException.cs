// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Proto;

/// <summary>
/// Schema 无法映射为 protobuf，或线上数据与 Schema 不符。消息为英文诊断文本；适配器把它翻译为错误码与本地化文案。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ProtoException : Exception
{
    /// <summary>
    /// 创建。
    /// </summary>
    public ProtoException()
    {
    }

    /// <summary>
    /// 创建，带诊断文本。
    /// </summary>
    public ProtoException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 创建，带诊断文本与原因。
    /// </summary>
    public ProtoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
