// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net.Http.Headers;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// AWS SDK 写出的 <c>Content-Type</c> 请求头
/// </summary>
/// <remarks>
/// SDK 把请求的内容类型交给 .NET 的 <see cref="MediaTypeHeaderValue"/> 解析后再写出：解析不了的（<c>;</c> 之后为空、未加引号的参数值里有空格等）在请求已经发出一半时抛出 <see cref="FormatException"/>；解析得了而写法不同的（<c>;</c> 两侧的空格）线上发的是改写后的值，签名却按原值计算，服务验签对不上。两类都写不出原值（RFC-0012「S3 Provider」，ADR-0295）
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
