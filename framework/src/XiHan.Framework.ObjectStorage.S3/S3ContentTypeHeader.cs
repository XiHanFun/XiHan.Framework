// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net.Http.Headers;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// AWS SDK 写出的 <c>Content-Type</c> 请求头
/// </summary>
/// <remarks>
/// SDK 把请求的内容类型交给 .NET 的 <see cref="MediaTypeHeaderValue"/> 解析后再写出：解析不了的（<c>;</c> 之后为空、未加引号的参数值里有空格等）抛出 <see cref="FormatException"/>，这时请求行与其余请求头已经发出；解析得了而写法不同的线上发的是改写后的值，签名（SigV4）却按原值去掉首尾空白、合并连续空格之后计算——合并之后仍与改写后的值不同的（<c>;</c> 两侧的空格与 <c>; </c> 不同）服务验签对不上，只差在连续空格的验签通过、服务存下改写后的值（对 RustFS 1.0.6 实测，Amazon S3 按 SigV4 的规则推断，ADR-0308）。几类都写不出原值（RFC-0012「S3 Provider」，ADR-0295）
/// </remarks>
internal static class S3ContentTypeHeader
{
    /// <summary>
    /// SDK 是否把内容类型原样写到线上
    /// </summary>
    /// <remarks>
    /// 照 SDK 的做法解析再写出，与原值逐字符相同才算原样
    /// </remarks>
    public static bool IsSentVerbatim(string contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out var parsed) && string.Equals(parsed.ToString(), contentType, StringComparison.Ordinal);
}
