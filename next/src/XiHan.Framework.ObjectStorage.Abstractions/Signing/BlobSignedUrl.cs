// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Signing;

/// <summary>
/// 签出的地址：客户端按 <see cref="Method"/> 请求 <see cref="Url"/>，并原样带上 <see cref="Headers"/>（如上传限定的 <c>Content-Type</c>）；过了 <see cref="ExpiresAt"/> 即失效。
/// 宿主代收发的地址可以是相对地址（相对应用的源）。
/// </summary>
/// <param name="Url">地址。</param>
/// <param name="Method">方法。</param>
/// <param name="ExpiresAt">失效时刻（UTC）。</param>
/// <param name="Headers">请求必须带的头。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobSignedUrl(Uri Url, BlobUrlMethod Method, DateTimeOffset ExpiresAt, IReadOnlyDictionary<string, string> Headers);
