// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Google.Protobuf;

namespace XiHan.Framework.Plugins.Conformance;

/// <summary>
/// 一致性用例发给插件的示例请求
/// </summary>
/// <remarks>
/// 示例请求是派生类给的字符串，按 RFC-0001「写出与转交」严格编码成 UTF-8：孤立的代理项编码不成，即派生类的配置有误，用例照配置错误失败，
/// 不像 <see cref="ByteString.CopyFromUtf8(string)"/> 那样换成 U+FFFD 再发出另一份请求（ADR-0327）
/// </remarks>
internal static class SamplePayloads
{
    /// <summary>
    /// 严格的 UTF-8 编码器：编码不成即抛出，不换成 U+FFFD
    /// </summary>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 把一个操作的示例请求编成载荷
    /// </summary>
    /// <param name="operation">操作 ID，只用于异常消息</param>
    /// <param name="sample">示例请求的 JSON 文本</param>
    /// <exception cref="InvalidOperationException">示例请求有孤立的代理项，编码不成 UTF-8</exception>
    public static ByteString Encode(string operation, string sample)
    {
        try
        {
            return UnsafeByteOperations.UnsafeWrap(_strictUtf8.GetBytes(sample));
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidOperationException($"The sample request of operation '{operation}' has a lone surrogate and cannot be encoded as UTF-8; fix SampleRequests.", exception);
        }
    }
}
