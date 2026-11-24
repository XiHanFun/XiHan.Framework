// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Signing;

/// <summary>
/// 签出的地址
/// </summary>
/// <remarks>
/// 客户端按 <see cref="Method"/> 请求 <see cref="Url"/>，并原样带上 <see cref="Headers"/>（如上传限定的 <c>Content-Type</c>）；过了 <see cref="ExpiresAt"/> 即失效。宿主代收发的地址可以是相对地址（相对应用的源）。四项都是必填的初始化成员，没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0266）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record BlobSignedUrl
{
    /// <summary>
    /// 地址
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// 方法
    /// </summary>
    public required BlobUrlMethod Method { get; init; }

    /// <summary>
    /// 失效时刻（UTC）
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// 请求必须带的头
    /// </summary>
    /// <remarks>
    /// 没有要带的头时是空字典
    /// </remarks>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
