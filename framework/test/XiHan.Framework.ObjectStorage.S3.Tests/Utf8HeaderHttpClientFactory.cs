// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Amazon.Runtime;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 把请求头按 UTF-8 原样写出的 SDK 传输
/// </summary>
/// <remarks>
/// 模拟不按 RFC 2047、直接把 UTF-8 字节写进 <c>x-amz-meta-*</c> 头的别的写入方：.NET 的 HTTP 客户端缺省不发出含非 ASCII 字符的请求头，<see cref="SocketsHttpHandler.RequestHeaderEncodingSelector"/> 选 UTF-8 才发得出；SDK 按同一串字符的 UTF-8 签名，服务按收到的字节验签对得上（ADR-0343）
/// </remarks>
internal sealed class Utf8HeaderHttpClientFactory : HttpClientFactory
{
    /// <inheritdoc />
    public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        => new(new SocketsHttpHandler { RequestHeaderEncodingSelector = static (_, _) => Encoding.UTF8 });
}
